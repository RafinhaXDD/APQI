using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace BookExchange.IntegrationTests.Infrastructure;

/// <summary>Real images generated in memory, including a JPEG that carries GPS coordinates in EXIF.</summary>
public static class TestImages
{
    public static byte[] Jpeg(int width = 800, int height = 600, bool withGps = false) => Encode(width, height, new JpegEncoder(), withGps);

    public static byte[] Png(int width = 400, int height = 300) => Encode(width, height, new PngEncoder(), withGps: false);

    public static byte[] Webp(int width = 400, int height = 300) => Encode(width, height, new WebpEncoder(), withGps: false);

    public static bool HasExif(byte[] bytes)
    {
        using var image = Image.Load(bytes);
        return image.Metadata.ExifProfile is not null;
    }

    public static (int Width, int Height, string Format) Identify(byte[] bytes)
    {
        var info = Image.Identify(bytes);
        return (info.Width, info.Height, info.Metadata.DecodedImageFormat!.Name);
    }

    private static byte[] Encode(int width, int height, IImageEncoder encoder, bool withGps)
    {
        using var image = new Image<Rgb24>(width, height, new Rgb24(47, 93, 80));
        if (withGps)
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.GPSLatitudeRef, "S");
            exif.SetValue(ExifTag.GPSLatitude, [new Rational(23, 1), new Rational(33, 1), new Rational(1, 1)]);
            exif.SetValue(ExifTag.GPSLongitudeRef, "W");
            exif.SetValue(ExifTag.GPSLongitude, [new Rational(46, 1), new Rational(38, 1), new Rational(2, 1)]);
            exif.SetValue(ExifTag.Make, "TestPhone");
            image.Metadata.ExifProfile = exif;
        }

        using var output = new MemoryStream();
        image.Save(output, encoder);
        return output.ToArray();
    }
}
