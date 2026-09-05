using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportingEngine.Application.Abstractions.Data;
using ReportingEngine.Application.Abstractions.Delivery;
using ReportingEngine.Application.Abstractions.Execution;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Application.Abstractions.Repositories;
using ReportingEngine.Application.Options;
using ReportingEngine.Domain.Entities;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Execution;

public sealed class JobExecutor : IJobExecutor
{
    private readonly IReportRepository _reportRepository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IFileExecutionRepository _fileExecutionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IParameterResolver _parameterResolver;
    private readonly IDataSourceProviderResolver _dataSourceProviderResolver;
    private readonly IFileSplitter _fileSplitter;
    private readonly IFileCompressor _fileCompressor;
    private readonly IDeliveryProviderResolver _deliveryProviderResolver;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAuditService _auditService;
    private readonly ExecutionOptions _executionOptions;
    private readonly RetryOptions _retryOptions;
    private readonly ILogger<JobExecutor> _logger;

    public JobExecutor(
        IReportRepository reportRepository,
        IJobExecutionRepository executionRepository,
        IFileExecutionRepository fileExecutionRepository,
        IUnitOfWork unitOfWork,
        IParameterResolver parameterResolver,
        IDataSourceProviderResolver dataSourceProviderResolver,
        IFileSplitter fileSplitter,
        IFileCompressor fileCompressor,
        IDeliveryProviderResolver deliveryProviderResolver,
        IIdempotencyService idempotencyService,
        IAuditService auditService,
        IOptions<ExecutionOptions> executionOptions,
        IOptions<RetryOptions> retryOptions,
        ILogger<JobExecutor> logger)
    {
        _reportRepository = reportRepository;
        _executionRepository = executionRepository;
        _fileExecutionRepository = fileExecutionRepository;
        _unitOfWork = unitOfWork;
        _parameterResolver = parameterResolver;
        _dataSourceProviderResolver = dataSourceProviderResolver;
        _fileSplitter = fileSplitter;
        _fileCompressor = fileCompressor;
        _deliveryProviderResolver = deliveryProviderResolver;
        _idempotencyService = idempotencyService;
        _auditService = auditService;
        _executionOptions = executionOptions.Value;
        _retryOptions = retryOptions.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(long reportId, DateTime? scheduledTimeUtc = null, bool isManual = false, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = _idempotencyService.BuildKey(reportId, scheduledTimeUtc, isManual);
        var claimed = await _idempotencyService.TryClaimAsync(idempotencyKey, reportId, scheduledTimeUtc, cancellationToken);
        if (!claimed)
        {
            _logger.LogInformation("Skipping duplicate execution for ReportId={ReportId} Key={Key}", reportId, idempotencyKey);
            return;
        }

        var execution = await _executionRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
            ?? throw new InvalidOperationException("Execution claim succeeded but execution row was not found.");

        await RunPipelineAsync(execution, cancellationToken);
    }

    public async Task ExecuteByExecutionIdAsync(long executionId, CancellationToken cancellationToken = default)
    {
        var execution = await _executionRepository.GetByIdWithFilesAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Execution {executionId} was not found.");

        await RunPipelineAsync(execution, cancellationToken);
    }

    private async Task RunPipelineAsync(JobExecution execution, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        execution.Status = JobExecutionStatuses.Running;
        execution.StartedAt ??= started;
        await _executionRepository.UpdateAsync(execution, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ExecutionId"] = execution.ExecutionId,
            ["ReportId"] = execution.ReportId
        });

        try
        {
            var report = await _reportRepository.GetByIdWithDetailsAsync(execution.ReportId, cancellationToken)
                ?? throw new InvalidOperationException($"Report {execution.ReportId} was not found.");

            if (execution.Status == JobExecutionStatuses.Cancelled)
            {
                return;
            }

            // Idempotent re-entry: if files already delivered, finalize without re-delivery.
            if (execution.Files.Count > 0 && execution.Files.All(f => f.DeliveryStatus == FileDeliveryStatuses.Delivered))
            {
                execution.Status = JobExecutionStatuses.Success;
                execution.CompletedAt = DateTime.UtcNow;
                execution.FileCount = execution.Files.Count;
                execution.RecordCount = execution.Files.Sum(f => f.RecordCount ?? 0);
                await _executionRepository.UpdateAsync(execution, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Recovered already-delivered execution {ExecutionId} without re-delivery", execution.ExecutionId);
                return;
            }

            var currentExecutionUtc = DateTime.UtcNow;
            var parameters = await _parameterResolver.ResolveAsync(report.ReportId, currentExecutionUtc, cancellationToken);

            var dataProvider = _dataSourceProviderResolver.Resolve(report.DataSource.DataSourceType);
            var rows = await dataProvider.ExecuteQueryAsync(
                report.DataSource.ConnectionReference,
                report.QueryText,
                parameters,
                cancellationToken);

            var outputDirectory = Path.Combine(
                _executionOptions.TemporaryFilePath,
                report.ReportCode,
                execution.ExecutionId.ToString());

            var tokenContext = new FileNameTokenContext(
                report.Customer.CustomerCode,
                report.ReportCode,
                execution.ExecutionId,
                currentExecutionUtc,
                Sequence: 1);

            var generatedFiles = await _fileSplitter.SplitAsync(
                rows,
                report.FileConfiguration.FileFormat,
                outputDirectory,
                report.FileConfiguration.FileNamePattern,
                tokenContext,
                report.FileConfiguration.SplitEnabled,
                report.FileConfiguration.SplitType,
                report.FileConfiguration.SplitValue,
                cancellationToken);

            var deliveryPaths = new List<string>();
            var sequence = 1;
            foreach (var generated in generatedFiles)
            {
                var path = generated.FilePath;
                if (!string.IsNullOrWhiteSpace(report.FileConfiguration.CompressionType))
                {
                    path = await _fileCompressor.CompressAsync(path, report.FileConfiguration.CompressionType, cancellationToken);
                }

                if (report.FileConfiguration.EncryptionEnabled)
                {
                    _logger.LogWarning("EncryptionEnabled is true but encryption provider is not configured; delivering unencrypted file.");
                }

                var fileExecution = execution.Files.FirstOrDefault(f => f.SequenceNumber == sequence);
                if (fileExecution is null)
                {
                    fileExecution = new FileExecution
                    {
                        ExecutionId = execution.ExecutionId,
                        SequenceNumber = sequence,
                        FileName = Path.GetFileName(path),
                        FilePath = path,
                        FileSizeBytes = new FileInfo(path).Length,
                        RecordCount = generated.RecordCount,
                        GenerationStatus = FileGenerationStatuses.Generated,
                        DeliveryStatus = FileDeliveryStatuses.Pending,
                        Checksum = generated.Checksum,
                        CreatedDate = DateTime.UtcNow
                    };
                    await _fileExecutionRepository.AddAsync(fileExecution, cancellationToken);
                }
                else
                {
                    fileExecution.FileName = Path.GetFileName(path);
                    fileExecution.FilePath = path;
                    fileExecution.FileSizeBytes = new FileInfo(path).Length;
                    fileExecution.RecordCount = generated.RecordCount;
                    fileExecution.GenerationStatus = FileGenerationStatuses.Generated;
                    fileExecution.Checksum = generated.Checksum;
                    await _fileExecutionRepository.UpdateAsync(fileExecution, cancellationToken);
                }

                if (fileExecution.DeliveryStatus != FileDeliveryStatuses.Delivered)
                {
                    deliveryPaths.Add(path);
                }

                sequence++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (deliveryPaths.Count > 0)
            {
                var deliveryProvider = _deliveryProviderResolver.Resolve(report.DeliveryConfiguration.DeliveryType);
                await deliveryProvider.DeliverAsync(new DeliveryRequest
                {
                    DeliveryType = report.DeliveryConfiguration.DeliveryType,
                    DestinationReference = report.DeliveryConfiguration.DestinationReference,
                    SecretReference = report.DeliveryConfiguration.SecretReference,
                    EmailTo = report.DeliveryConfiguration.EmailTo,
                    EmailCc = report.DeliveryConfiguration.EmailCc,
                    EmailBcc = report.DeliveryConfiguration.EmailBcc,
                    EmailSubjectTemplate = report.DeliveryConfiguration.EmailSubjectTemplate,
                    EmailBodyTemplate = report.DeliveryConfiguration.EmailBodyTemplate,
                    AttachmentPaths = deliveryPaths,
                    Tokens = new DeliveryTokenContext(
                        report.Customer.CustomerCode,
                        report.ReportCode,
                        currentExecutionUtc,
                        generatedFiles.Sum(f => f.RecordCount),
                        generatedFiles.Count)
                }, cancellationToken);

                var files = await _fileExecutionRepository.GetByExecutionIdAsync(execution.ExecutionId, cancellationToken);
                foreach (var file in files)
                {
                    file.DeliveryStatus = FileDeliveryStatuses.Delivered;
                    await _fileExecutionRepository.UpdateAsync(file, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            execution.Status = JobExecutionStatuses.Success;
            execution.CompletedAt = DateTime.UtcNow;
            execution.RecordCount = generatedFiles.Sum(f => f.RecordCount);
            execution.FileCount = generatedFiles.Count;
            execution.ErrorCode = null;
            execution.ErrorMessage = null;
            await _executionRepository.UpdateAsync(execution, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var durationMs = (execution.CompletedAt.Value - execution.StartedAt!.Value).TotalMilliseconds;
            _logger.LogInformation(
                "Execution completed. CustomerId={CustomerId} Status={Status} RecordCount={RecordCount} FileCount={FileCount} DurationMs={DurationMs} RetryCount={RetryCount}",
                report.CustomerId, execution.Status, execution.RecordCount, execution.FileCount, durationMs, execution.RetryCount);

            await _auditService.WriteAsync("JobExecution", execution.ExecutionId, "SUCCESS", "system", cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var isTransient = IsTransient(ex);
            execution.ErrorCode = isTransient ? "TRANSIENT" : "PERMANENT";
            execution.ErrorMessage = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;

            if (isTransient && execution.RetryCount < _retryOptions.MaxRetryCount)
            {
                execution.Status = JobExecutionStatuses.Retrying;
                execution.RetryCount += 1;
                await _executionRepository.UpdateAsync(execution, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex,
                    "Execution failed transiently. Status=RETRYING RetryCount={RetryCount}",
                    execution.RetryCount);
                throw; // Let Hangfire retry with backoff
            }

            execution.Status = JobExecutionStatuses.Failed;
            await _executionRepository.UpdateAsync(execution, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Execution failed permanently. Status=FAILED RetryCount={RetryCount}", execution.RetryCount);
            await _auditService.WriteAsync("JobExecution", execution.ExecutionId, "FAILED", "system", newValue: new { ex.Message }, cancellationToken: cancellationToken);

            if (!isTransient)
            {
                // Permanent errors should not endlessly retry via Hangfire.
                return;
            }

            throw;
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException
        or IOException
        or Microsoft.Data.SqlClient.SqlException { Number: 1205 or -2 or 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 };
}
