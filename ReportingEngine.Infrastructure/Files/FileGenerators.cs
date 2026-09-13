using ReportingEngine.Application.Abstractions.Files;
using ReportingEngine.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace ReportingEngine.Infrastructure.Files;

public sealed class CsvFileGenerator : IFileGenerator
{
    public string Format => FileFormats.Csv;

    public async Task<GeneratedFileResult> GenerateAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        long recordCount = 0;
        string[]? headers = null;

        await using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                if (headers is null)
                {
                    headers = row.Keys.ToArray();
                    await writer.WriteLineAsync(string.Join(",", headers.Select(Escape)));
                }

                var values = headers.Select(h => Escape(row.TryGetValue(h, out var v) ? v : null));
                await writer.WriteLineAsync(string.Join(",", values));
                recordCount++;
            }

            if (headers is null)
            {
                await writer.WriteLineAsync("Empty");
            }
        }

        var info = new FileInfo(outputPath);
        return new GeneratedFileResult(outputPath, recordCount, info.Length, ComputeSha256(outputPath));
    }

    private static string Escape(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    internal static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}

public sealed class JsonFileGenerator : IFileGenerator
{
    public string Format => FileFormats.Json;

    public async Task<GeneratedFileResult> GenerateAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        long recordCount = 0;
        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync('[');
        var first = true;
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            if (!first)
            {
                await writer.WriteAsync(',');
            }

            first = false;
            await writer.WriteAsync(System.Text.Json.JsonSerializer.Serialize(row));
            recordCount++;
        }

        await writer.WriteAsync(']');
        await writer.FlushAsync(cancellationToken);
        var info = new FileInfo(outputPath);
        return new GeneratedFileResult(outputPath, recordCount, info.Length, CsvFileGenerator.ComputeSha256(outputPath));
    }
}

public sealed class TxtFileGenerator : IFileGenerator
{
    public string Format => FileFormats.Txt;

    public async Task<GeneratedFileResult> GenerateAsync(
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        long recordCount = 0;
        string[]? headers = null;
        await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            if (headers is null)
            {
                headers = row.Keys.ToArray();
                await writer.WriteLineAsync(string.Join('\t', headers));
            }

            await writer.WriteLineAsync(string.Join('\t', headers.Select(h => row.TryGetValue(h, out var v) ? v?.ToString() ?? string.Empty : string.Empty)));
            recordCount++;
        }

        var info = new FileInfo(outputPath);
        return new GeneratedFileResult(outputPath, recordCount, info.Length, CsvFileGenerator.ComputeSha256(outputPath));
    }
}

public sealed class ExcelFileGenerator : IFileGenerator
{
    public string Format => FileFormats.Excel;
    public Task<GeneratedFileResult> GenerateAsync(IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows, string outputPath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("EXCEL file generation is scaffolded but not implemented yet. Use CSV.");
}

public sealed class XmlFileGenerator : IFileGenerator
{
    public string Format => FileFormats.Xml;
    public Task<GeneratedFileResult> GenerateAsync(IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows, string outputPath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("XML file generation is scaffolded but not implemented yet. Use CSV.");
}

public sealed class FileGeneratorResolver : IFileGeneratorResolver
{
    private readonly IReadOnlyDictionary<string, IFileGenerator> _generators;

    public FileGeneratorResolver(IEnumerable<IFileGenerator> generators) =>
        _generators = generators.ToDictionary(g => g.Format, StringComparer.OrdinalIgnoreCase);

    public bool IsSupported(string fileFormat) =>
        string.Equals(fileFormat, FileFormats.Csv, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileFormat, FileFormats.Json, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileFormat, FileFormats.Txt, StringComparison.OrdinalIgnoreCase);

    public IFileGenerator Resolve(string fileFormat)
    {
        if (_generators.TryGetValue(fileFormat, out var generator))
        {
            return generator;
        }

        throw new NotSupportedException($"File format '{fileFormat}' is not supported.");
    }
}
