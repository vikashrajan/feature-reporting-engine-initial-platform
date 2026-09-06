namespace ReportingEngine.Application.Abstractions.Files;

public interface IFileGenerator
{
    string Format { get; }
    Task<GeneratedFileResult> GenerateAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string outputPath,
        CancellationToken cancellationToken = default);
}

public interface IFileGeneratorResolver
{
    IFileGenerator Resolve(string fileFormat);
    bool IsSupported(string fileFormat);
}

public sealed record GeneratedFileResult(string FilePath, long RecordCount, long FileSizeBytes, string Checksum);

public interface IFileSplitter
{
    Task<IReadOnlyList<GeneratedFileResult>> SplitAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string format,
        string outputDirectory,
        string fileNamePattern,
        FileNameTokenContext tokenContext,
        bool splitEnabled,
        string? splitType,
        long? splitValue,
        CancellationToken cancellationToken = default);
}

public interface IFileNameTokenReplacer
{
    string Replace(string pattern, FileNameTokenContext context);
}

public sealed record FileNameTokenContext(
    string CustomerCode,
    string ReportCode,
    long ExecutionId,
    DateTime ExecutionDateUtc,
    int Sequence);

public interface IFileCompressor
{
    Task<string> CompressAsync(string sourceFilePath, string compressionType, CancellationToken cancellationToken = default);
    Task<string> CompressMultipleAsync(IEnumerable<string> sourceFilePaths, string zipDestinationPath, CancellationToken cancellationToken = default);
}
