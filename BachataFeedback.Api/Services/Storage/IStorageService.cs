namespace BachataFeedback.Api.Services.Storage;

public interface IStorageService
{
    Task EnsureBucketAsync(CancellationToken ct = default);
    Task PutObjectAsync(string key, Stream data, string contentType, CancellationToken ct = default);
    Task<Stream> GetObjectAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteObjectAsync(string key, CancellationToken ct = default);
}