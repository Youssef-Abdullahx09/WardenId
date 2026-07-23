using FluentResults;

namespace WardenId.SharedKernel.FileOperation;

public interface IReadFileService
{
    Task<Result<string>> ReadFileContentAsync(
        string relativePath,
        CancellationToken cancellationToken);
}