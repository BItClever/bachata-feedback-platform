namespace BachataFeedback.Api.Services.Antivirus;

public interface IAntivirusScanner
{
    Task<bool> IsCleanAsync(Stream file, CancellationToken ct = default);
}