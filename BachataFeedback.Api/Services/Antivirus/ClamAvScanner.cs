using nClam;

namespace BachataFeedback.Api.Services.Antivirus;

public class ClamAvScanner : IAntivirusScanner
{
    private readonly string _host;
    private readonly int _port;

    public ClamAvScanner(IConfiguration cfg)
    {
        _host = cfg.GetValue<string>("Antivirus:ClamAV:Host") ?? "localhost";
        _port = cfg.GetValue<int?>("Antivirus:ClamAV:Port") ?? 3310;
    }

    public async Task<bool> IsCleanAsync(Stream file, CancellationToken ct = default)
    {
        // Копируем в память, чтобы можно было переиспользовать
        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;

        var clam = new ClamClient(_host, _port);
        var result = await clam.SendAndScanFileAsync(ms, ct);

        // Вернёмся к началу, чтобы дальше читать
        ms.Position = 0;

        return result.Result switch
        {
            ClamScanResults.Clean => true,
            _ => false
        };
    }
}