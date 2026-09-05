using System.IO.Compression;
using System.Runtime.CompilerServices;
using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Domain.Enums;

namespace ReportingEngine.Infrastructure.Files;

public sealed class FileNameTokenReplacer : IFileNameTokenReplacer
{
    public string Replace(string pattern, FileNameTokenContext context)
    {
        var local = context.ExecutionDateUtc;
        return pattern
            .Replace("{CustomerCode}", context.CustomerCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{ReportCode}", context.ReportCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{ExecutionId}", context.ExecutionId.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{ExecutionDate}", local.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Sequence}", context.Sequence.ToString("000"), StringComparison.OrdinalIgnoreCase)
            .Replace("{yyyyMMdd}", local.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{yyyyMMddHHmmss}", local.ToString("yyyyMMddHHmmss"), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FileCompressor : IFileCompressor
{
    public Task<string> CompressAsync(string sourceFilePath, string compressionType, CancellationToken cancellationToken = default)
    {
        var type = compressionType.Trim().ToUpperInvariant();
        return type switch
        {
            CompressionTypes.Zip => Task.FromResult(CompressZip(sourceFilePath)),
            CompressionTypes.Gzip => Task.FromResult(CompressGzip(sourceFilePath)),
            _ => throw new NotSupportedException($"Compression type '{compressionType}' is not supported.")
        };
    }

    private static string CompressZip(string sourceFilePath)
    {
        var zipPath = sourceFilePath + ".zip";
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(sourceFilePath, Path.GetFileName(sourceFilePath), CompressionLevel.Optimal);
        return zipPath;
    }

    private static string CompressGzip(string sourceFilePath)
    {
        var gzipPath = sourceFilePath + ".gz";
        using var input = File.OpenRead(sourceFilePath);
        using var output = File.Create(gzipPath);
        using var gzip = new GZipStream(output, CompressionLevel.Optimal);
        input.CopyTo(gzip);
        return gzipPath;
    }
}

public sealed class FileSplitter : IFileSplitter
{
    private readonly IFileGeneratorResolver _generatorResolver;
    private readonly IFileNameTokenReplacer _tokenReplacer;

    public FileSplitter(IFileGeneratorResolver generatorResolver, IFileNameTokenReplacer tokenReplacer)
    {
        _generatorResolver = generatorResolver;
        _tokenReplacer = tokenReplacer;
    }

    public async Task<IReadOnlyList<GeneratedFileResult>> SplitAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string format,
        string outputDirectory,
        string fileNamePattern,
        FileNameTokenContext tokenContext,
        bool splitEnabled,
        string? splitType,
        long? splitValue,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var generator = _generatorResolver.Resolve(format);

        if (!splitEnabled || splitValue is null or <= 0)
        {
            var fileName = _tokenReplacer.Replace(fileNamePattern, tokenContext with { Sequence = 1 });
            var path = Path.Combine(outputDirectory, fileName);
            var result = await generator.GenerateAsync(rows, path, cancellationToken);
            return new[] { result };
        }

        var type = splitType?.Trim().ToUpperInvariant();
        return type switch
        {
            SplitTypes.RecordCount => await SplitByRecordCountAsync(rows, generator, outputDirectory, fileNamePattern, tokenContext, splitValue.Value, cancellationToken),
            SplitTypes.FileSize => await SplitByFileSizeAsync(rows, generator, outputDirectory, fileNamePattern, tokenContext, splitValue.Value, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported split type '{splitType}'.")
        };
    }

    private async Task<IReadOnlyList<GeneratedFileResult>> SplitByRecordCountAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        IFileGenerator generator,
        string outputDirectory,
        string fileNamePattern,
        FileNameTokenContext tokenContext,
        long recordLimit,
        CancellationToken cancellationToken)
    {
        var results = new List<GeneratedFileResult>();
        var buffer = new List<IReadOnlyDictionary<string, object?>>();
        var sequence = 1;

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            buffer.Add(row);
            if (buffer.Count >= recordLimit)
            {
                results.Add(await WriteBufferAsync(generator, buffer, outputDirectory, fileNamePattern, tokenContext, sequence++, cancellationToken));
                buffer.Clear();
            }
        }

        if (buffer.Count > 0 || results.Count == 0)
        {
            results.Add(await WriteBufferAsync(generator, buffer, outputDirectory, fileNamePattern, tokenContext, sequence, cancellationToken));
        }

        return results;
    }

    private async Task<IReadOnlyList<GeneratedFileResult>> SplitByFileSizeAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        IFileGenerator generator,
        string outputDirectory,
        string fileNamePattern,
        FileNameTokenContext tokenContext,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var results = new List<GeneratedFileResult>();
        var buffer = new List<IReadOnlyDictionary<string, object?>>();
        var sequence = 1;
        long estimatedBytes = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            var rowBytes = EstimateRowBytes(row);
            if (buffer.Count > 0 && estimatedBytes + rowBytes > maxBytes)
            {
                results.Add(await WriteBufferAsync(generator, buffer, outputDirectory, fileNamePattern, tokenContext, sequence++, cancellationToken));
                buffer.Clear();
                estimatedBytes = 0;
            }

            buffer.Add(row);
            estimatedBytes += rowBytes;
        }

        if (buffer.Count > 0 || results.Count == 0)
        {
            results.Add(await WriteBufferAsync(generator, buffer, outputDirectory, fileNamePattern, tokenContext, sequence, cancellationToken));
        }

        return results;
    }

    private async Task<GeneratedFileResult> WriteBufferAsync(
        IFileGenerator generator,
        List<IReadOnlyDictionary<string, object?>> buffer,
        string outputDirectory,
        string fileNamePattern,
        FileNameTokenContext tokenContext,
        int sequence,
        CancellationToken cancellationToken)
    {
        var fileName = _tokenReplacer.Replace(fileNamePattern, tokenContext with { Sequence = sequence });
        var path = Path.Combine(outputDirectory, fileName);
        return await generator.GenerateAsync(ToAsync(buffer), path, cancellationToken);
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ToAsync(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
            await Task.Yield();
        }
    }

    private static long EstimateRowBytes(IReadOnlyDictionary<string, object?> row) =>
        row.Sum(kv => (kv.Key?.Length ?? 0) + (kv.Value?.ToString()?.Length ?? 0) + 2);
}
