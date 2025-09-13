namespace BachataFeedback.Api.Services.Images;

public interface IImageProcessor
{
    Task<Dictionary<string, (MemoryStream data, string contentType)>> ProcessAsync(Stream original, CancellationToken ct = default);
}