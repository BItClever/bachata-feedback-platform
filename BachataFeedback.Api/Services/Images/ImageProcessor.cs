using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;

namespace BachataFeedback.Api.Services.Images;

public class ImageProcessor : IImageProcessor
{
    public async Task<Dictionary<string, (MemoryStream data, string contentType)>> ProcessAsync(Stream original, CancellationToken ct = default)
    {
        original.Position = 0;
        using var image = await Image.LoadAsync(original, ct);

        // Удаляем EXIF
        image.Metadata.ExifProfile = null;

        var variants = new Dictionary<string, (MemoryStream, string)>();

        // Параметры JPEG
        var encoder = new JpegEncoder { Quality = 85 };
        string contentType = "image/jpeg";

        // Вспомогательный локальный метод
        async Task<(MemoryStream, string)> RenderAsync(int? maxWidth, int? maxHeight)
        {
            using var clone = image.Clone(ctx =>
            {
                if (maxWidth.HasValue || maxHeight.HasValue)
                {
                    var size = new Size(maxWidth ?? 0, maxHeight ?? 0);
                    ctx.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = size
                    });
                }
            });

            var ms = new MemoryStream();
            await clone.SaveAsJpegAsync(ms, encoder, ct);
            ms.Position = 0;
            return (ms, contentType);
        }

        variants["small"] = await RenderAsync(256, 256);
        variants["medium"] = await RenderAsync(800, 800);
        variants["large"] = await RenderAsync(1600, 1600);

        // original (перекодируем для очистки метаданных и ограничим до 3000 px по длинной стороне)
        variants["original"] = await RenderAsync(3000, 3000);

        return variants;
    }
}