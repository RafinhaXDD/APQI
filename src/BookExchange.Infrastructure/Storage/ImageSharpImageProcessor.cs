using BookExchange.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace BookExchange.Infrastructure.Storage;

/// <summary>
/// SPEC §6.10: the format is detected from the bytes (magic numbers), never the file name or Content-Type.
/// Photos are auto-rotated, stripped of every metadata profile (EXIF/GPS, IPTC, XMP, ICC) and re-encoded
/// as WebP in a display size and a thumbnail.
/// </summary>
internal sealed class ImageSharpImageProcessor : IImageProcessor
{
    public const int DisplayMaxSide = 1280;
    public const int ThumbnailMaxSide = 320;

    /// <summary>Guards against decompression bombs: a 5 MB file can claim enormous dimensions.</summary>
    public const long MaxPixels = 40_000_000;

    private static readonly IImageFormat[] Allowed = [JpegFormat.Instance, PngFormat.Instance, WebpFormat.Instance];
    private static readonly WebpEncoder Encoder = new() { Quality = 80 };

    public async Task<ProcessedImage> ProcessAsync(Stream upload, CancellationToken cancellationToken)
    {
        // Buffer once: detection, identification and decoding each read from the start.
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, cancellationToken);

        try
        {
            buffer.Position = 0;
            var format = await Image.DetectFormatAsync(buffer, cancellationToken);
            if (!Allowed.Contains(format))
            {
                throw new InvalidImageException($"Format {format.Name} is not allowed.");
            }

            buffer.Position = 0;
            var info = await Image.IdentifyAsync(buffer, cancellationToken);
            if ((long)info.Width * info.Height > MaxPixels)
            {
                throw new InvalidImageException("Image dimensions are too large.");
            }

            buffer.Position = 0;
            using var image = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, buffer, cancellationToken);
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;

            var display = await EncodeAsync(image, DisplayMaxSide, cancellationToken);
            var thumbnail = await EncodeAsync(image, ThumbnailMaxSide, cancellationToken);
            return new ProcessedImage(display.Bytes, thumbnail.Bytes, "image/webp", ".webp", display.Width, display.Height);
        }
        catch (Exception e) when (e is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new InvalidImageException("Not a supported image.", e);
        }
    }

    private static async Task<(byte[] Bytes, int Width, int Height)> EncodeAsync(Image source, int maxSide, CancellationToken cancellationToken)
    {
        using var copy = source.Clone(x =>
        {
            if (source.Width > maxSide || source.Height > maxSide)
            {
                x.Resize(new ResizeOptions { Size = new Size(maxSide, maxSide), Mode = ResizeMode.Max });
            }
        });
        using var output = new MemoryStream();
        await copy.SaveAsync(output, Encoder, cancellationToken);
        return (output.ToArray(), copy.Width, copy.Height);
    }
}
