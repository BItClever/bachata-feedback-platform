using Minio;
using Minio.DataModel.Args;

namespace BachataFeedback.Api.Services.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucket;

    public MinioStorageService(IConfiguration cfg)
    {
        var section = cfg.GetSection("Storage");
        var endpoint = section.GetValue<string>("Endpoint")!;
        var access = section.GetValue<string>("AccessKey")!;
        var secret = section.GetValue<string>("SecretKey")!;
        var useSsl = section.GetValue<bool>("UseSSL");
        _bucket = section.GetValue<string>("Bucket")!;

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(access, secret)
            .WithSSL(useSsl)
            .Build();
    }

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        var beArgs = new BucketExistsArgs().WithBucket(_bucket);
        var exists = await _client.BucketExistsAsync(beArgs, ct);
        if (!exists)
        {
            var mkArgs = new MakeBucketArgs().WithBucket(_bucket);
            await _client.MakeBucketAsync(mkArgs, ct);
        }
    }

    public async Task PutObjectAsync(string key, Stream data, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        data.Position = 0;
        var put = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType);
        await _client.PutObjectAsync(put, ct);
    }

    public async Task<Stream> GetObjectAsync(string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        var get = new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithCallbackStream(stream => stream.CopyTo(ms));
        await _client.GetObjectAsync(get, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var stat = new StatObjectArgs().WithBucket(_bucket).WithObject(key);
            await _client.StatObjectAsync(stat, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DeleteObjectAsync(string key, CancellationToken ct = default)
    {
        var del = new RemoveObjectArgs().WithBucket(_bucket).WithObject(key);
        await _client.RemoveObjectAsync(del, ct);
    }
}