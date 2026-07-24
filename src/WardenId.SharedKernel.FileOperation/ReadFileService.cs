using FluentResults;
using Microsoft.Extensions.Logging;

namespace WardenId.SharedKernel.FileOperation;

public sealed class ReadFileService(
    ILogger<ReadFileService> logger, 
    string? rootDirectory = null) : IReadFileService
{
    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory ?? AppDomain.CurrentDomain.BaseDirectory)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // Inject the root instead of hardcoding it inline — makes this testable against a
    // temp directory, and lets different callers point at different roots if needed.

    public async Task<Result<string>> ReadFileContentAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Result.Fail(ErrorMessage.InvalidFilePath);

        // Accept both '/' and '\' from callers so paths read naturally regardless of
        // how they were typed (constants files, config, etc.) and normalize internally.
        var routeSegments = relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

        if (routeSegments.Length == 0 || routeSegments.Any(string.IsNullOrWhiteSpace))
        {
            return Result.Fail(ErrorMessage.FileNotFound);
        }

        string filePath;
        try
        {
            filePath = ResolveSafePath(routeSegments);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException)
        {
            // Same error as "not found" is intentional: don't leak to callers whether
            // a request was rejected for traversal vs. genuinely missing — it avoids
            // giving anyone probing the API a way to distinguish the two cases.
            logger.LogWarning(ex, "Rejected file route: {RelativePath}", relativePath);
            return Result.Fail(ErrorMessage.FileNotFound);
        }

        if (!File.Exists(filePath)) return Result.Fail(ErrorMessage.FileNotFound);

        try
        {
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return Result.Ok(fileContent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to read file at {Path}", filePath);
            return Result.Fail(ErrorMessage.FileNotFound);
        }
    }

    /// <summary>
    /// Combines route segments under the configured root and verifies the resolved path
    /// still lives inside it. Blocks "../" traversal and rejects any segment that is itself
    /// a rooted/absolute path (which would otherwise make Path.Combine jump elsewhere on disk).
    /// </summary>
    private string ResolveSafePath(IReadOnlyCollection<string> routeSegments)
    {
        if (routeSegments.Any(Path.IsPathRooted))
        {
            throw new UnauthorizedAccessException("Rooted path segments are not allowed.");
        }

        var combined = routeSegments.Aggregate(_rootDirectory, Path.Combine);
        var fullPath = Path.GetFullPath(combined);

        var isInsideRoot =
            fullPath.Equals(_rootDirectory, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(_rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        if (!isInsideRoot)
        {
            throw new UnauthorizedAccessException("Resolved path escapes the allowed root directory.");
        }

        return fullPath;
    }
}