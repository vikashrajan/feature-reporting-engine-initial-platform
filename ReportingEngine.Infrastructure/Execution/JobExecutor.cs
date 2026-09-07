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
    private readonly EmailOptions _emailOptions;
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
        IOptions<EmailOptions> emailOptions,
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
        _emailOptions = emailOptions.Value;
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
                if (!report.FileConfiguration.KeepLocalFiles)
                {
                    DeleteLocalFiles(execution.Files.Where(f => f.FilePath is not null).Select(f => f.FilePath!), BuildOutputDirectory(report, execution));
                    await ClearLocalFilePathsAsync(execution.ExecutionId, cancellationToken);
                }
                _logger.LogInformation("Recovered already-delivered execution {ExecutionId} without re-delivery", execution.ExecutionId);
                return;
            }

            var currentExecutionUtc = DateTime.UtcNow;
            var parameters = await _parameterResolver.ResolveAsync(report.ReportId, currentExecutionUtc, cancellationToken);

            var dataProvider = _dataSourceProviderResolver.Resolve(report.DataSource.DataSourceType);
            var rows = await dataProvider.ExecuteQueryAsync(
                string.IsNullOrWhiteSpace(report.DataSource.ConnectionString)
                    ? report.DataSource.ConnectionReference
                    : report.DataSource.ConnectionString,
                report.QueryText,
                parameters,
                cancellationToken);

            var outputDirectory = BuildOutputDirectory(report, execution);

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

            var sequence = 1;
            var uncompressedFilePaths = new List<string>();
            var deliveredAttachmentPaths = new List<string>();
            var generatedFileBatches = new List<(int SequenceNumber, string FilePath, long RecordCount)>();

            foreach (var generated in generatedFiles)
            {
                var path = generated.FilePath;
                uncompressedFilePaths.Add(path);
                generatedFileBatches.Add((sequence, path, generated.RecordCount));

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

                sequence++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(report.FileConfiguration.CompressionType) &&
                string.Equals(report.FileConfiguration.CompressionType.Trim(), CompressionTypes.Zip, StringComparison.OrdinalIgnoreCase))
            {
                var zipBatchSize = report.FileConfiguration.ZipBatchSize.GetValueOrDefault();
                var zipBatches = zipBatchSize > 0
                    ? generatedFileBatches.Chunk(zipBatchSize).ToList()
                    : new[] { generatedFileBatches.ToArray() }.ToList();

                var batchNumber = 1;
                foreach (var batch in zipBatches)
                {
                    var zipFileName = zipBatches.Count == 1
                        ? $"{report.ReportCode}_{report.Customer.CustomerCode}_{execution.ExecutionId}_{currentExecutionUtc:yyyyMMddHHmmss}.zip"
                        : $"{report.ReportCode}_{report.Customer.CustomerCode}_{execution.ExecutionId}_{currentExecutionUtc:yyyyMMddHHmmss}_batch{batchNumber:000}.zip";
                    var zipPath = Path.Combine(outputDirectory, zipFileName);
                    await _fileCompressor.CompressMultipleAsync(batch.Select(f => f.FilePath), zipPath, cancellationToken);
                    deliveredAttachmentPaths.Add(zipPath);
                    _logger.LogInformation(
                        "Zipped generated files {FirstSequence}-{LastSequence} into archive {ZipPath}",
                        batch.Min(f => f.SequenceNumber),
                        batch.Max(f => f.SequenceNumber),
                        zipPath);

                    await DeliverAsync(report, currentExecutionUtc, new[] { zipPath }, batch.Sum(f => f.RecordCount), batch.Length, cancellationToken);
                    await MarkFilesDeliveredAsync(execution.ExecutionId, batch.Select(f => f.SequenceNumber).ToHashSet(), cancellationToken);
                    batchNumber++;
                }
            }
            else
            {
                await DeliverAsync(report, currentExecutionUtc, uncompressedFilePaths, generatedFiles.Sum(f => f.RecordCount), generatedFiles.Count, cancellationToken);
                await MarkFilesDeliveredAsync(execution.ExecutionId, generatedFileBatches.Select(f => f.SequenceNumber).ToHashSet(), cancellationToken);
            }

            execution.Status = JobExecutionStatuses.Success;
            execution.CompletedAt = DateTime.UtcNow;
            execution.RecordCount = generatedFiles.Sum(f => f.RecordCount);
            execution.FileCount = generatedFiles.Count;
            execution.ErrorCode = null;
            execution.ErrorMessage = null;
            await _executionRepository.UpdateAsync(execution, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (!report.FileConfiguration.KeepLocalFiles)
            {
                DeleteLocalFiles(uncompressedFilePaths.Concat(deliveredAttachmentPaths), outputDirectory);
                await ClearLocalFilePathsAsync(execution.ExecutionId, cancellationToken);
            }

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
            await SendFailureNotificationAsync(execution, ex, cancellationToken);

            if (!isTransient)
            {
                // Permanent errors should not endlessly retry via Hangfire.
                return;
            }

            throw;
        }
    }

    private async Task DeliverAsync(
        ReportDefinition report,
        DateTime executionDateUtc,
        IReadOnlyList<string> attachmentPaths,
        long recordCount,
        int fileCount,
        CancellationToken cancellationToken)
    {
        if (attachmentPaths.Count == 0)
        {
            return;
        }

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
            AttachmentPaths = attachmentPaths,
            Tokens = new DeliveryTokenContext(
                report.Customer.CustomerCode,
                report.ReportCode,
                executionDateUtc,
                recordCount,
                fileCount)
        }, cancellationToken);
    }

    private async Task SendFailureNotificationAsync(JobExecution execution, Exception error, CancellationToken cancellationToken)
    {
        if (!_emailOptions.FailureNotificationEnabled || string.IsNullOrWhiteSpace(_emailOptions.FailureNotificationTo))
        {
            return;
        }

        ReportDefinition? report = null;
        try
        {
            report = await _reportRepository.GetByIdWithDetailsAsync(execution.ReportId, cancellationToken);
        }
        catch (Exception lookupError) when (lookupError is not OperationCanceledException)
        {
            _logger.LogWarning(lookupError, "Could not load report details for failure notification on execution {ExecutionId}", execution.ExecutionId);
        }

        try
        {
            var deliveryProvider = _deliveryProviderResolver.Resolve(DeliveryTypes.Email);
            await deliveryProvider.DeliverAsync(new DeliveryRequest
            {
                DeliveryType = DeliveryTypes.Email,
                EmailTo = _emailOptions.FailureNotificationTo,
                EmailCc = _emailOptions.FailureNotificationCc,
                EmailSubjectTemplate = _emailOptions.FailureNotificationSubjectTemplate,
                EmailBodyTemplate = _emailOptions.FailureNotificationBodyTemplate,
                AttachmentPaths = Array.Empty<string>(),
                Tokens = new DeliveryTokenContext(
                    report?.Customer?.CustomerCode ?? string.Empty,
                    report?.ReportCode ?? execution.ReportId.ToString(),
                    DateTime.UtcNow,
                    execution.RecordCount ?? 0,
                    execution.FileCount ?? 0,
                    execution.ExecutionId,
                    execution.Status,
                    error.Message)
            }, cancellationToken);
        }
        catch (Exception notificationError) when (notificationError is not OperationCanceledException)
        {
            _logger.LogWarning(notificationError, "Could not send failure notification email for execution {ExecutionId}", execution.ExecutionId);
        }
    }

    private async Task MarkFilesDeliveredAsync(long executionId, HashSet<int> sequenceNumbers, CancellationToken cancellationToken)
    {
        if (sequenceNumbers.Count == 0)
        {
            return;
        }

        var files = await _fileExecutionRepository.GetByExecutionIdAsync(executionId, cancellationToken);
        foreach (var file in files.Where(f => sequenceNumbers.Contains(f.SequenceNumber)))
        {
            file.DeliveryStatus = FileDeliveryStatuses.Delivered;
            await _fileExecutionRepository.UpdateAsync(file, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearLocalFilePathsAsync(long executionId, CancellationToken cancellationToken)
    {
        var files = await _fileExecutionRepository.GetByExecutionIdAsync(executionId, cancellationToken);
        foreach (var file in files.Where(f => !string.IsNullOrWhiteSpace(f.FilePath)))
        {
            file.FilePath = null;
            await _fileExecutionRepository.UpdateAsync(file, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void DeleteLocalFiles(IEnumerable<string> filePaths, string outputDirectory)
    {
        var root = Path.GetFullPath(outputDirectory);
        var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            filesToDelete.Add(filePath);
        }

        try
        {
            var tempRoot = Path.GetFullPath(_executionOptions.TemporaryFilePath);
            if (Directory.Exists(root)
                && IsPathInside(root, tempRoot)
                && !string.Equals(root, tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                {
                    filesToDelete.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate local execution output directory {OutputDirectory}", outputDirectory);
        }

        foreach (var filePath in filesToDelete)
        {
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!IsPathInside(fullPath, root))
                {
                    _logger.LogWarning("Skipped local cleanup outside execution directory: {FilePath}", filePath);
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete local generated file {FilePath}", filePath);
            }
        }

        TryDeleteEmptyOutputDirectory(root);
    }

    private string BuildOutputDirectory(ReportDefinition report, JobExecution execution) =>
        Path.Combine(_executionOptions.TemporaryFilePath, report.ReportCode, execution.ExecutionId.ToString());

    private void TryDeleteEmptyOutputDirectory(string outputDirectory)
    {
        try
        {
            var tempRoot = Path.GetFullPath(_executionOptions.TemporaryFilePath);
            if (!IsPathInside(outputDirectory, tempRoot) || string.Equals(outputDirectory, tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Skipped local cleanup outside report temp root: {OutputDirectory}", outputDirectory);
                return;
            }

            if (Directory.Exists(outputDirectory) && !Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                Directory.Delete(outputDirectory, recursive: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete empty local execution output directory {OutputDirectory}", outputDirectory);
        }
    }

    private static bool IsPathInside(string childPath, string parentPath)
    {
        var relative = Path.GetRelativePath(parentPath, childPath);
        return relative != "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException
        or IOException
        or Microsoft.Data.SqlClient.SqlException { Number: 1205 or -2 or 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 };
}
