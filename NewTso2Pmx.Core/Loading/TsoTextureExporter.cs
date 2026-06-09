using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using TDCG;

namespace NewTso2Pmx.Core.Loading;

public static class TsoTextureExporter
{
    private static readonly BmpEncoder BmpWithTransparencyEncoder = new()
    {
        BitsPerPixel = BmpBitsPerPixel.Pixel32,
        SupportTransparency = true
    };

    public static int SaveAll(LoadedDocument document, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var count = 0;
        for (var tsoIndex = 0; tsoIndex < document.Figure.TSOList.Count; tsoIndex++)
        {
            var tso = document.Figure.TSOList[tsoIndex];
            var category = document.Categories.Count > tsoIndex
                ? document.Categories[tsoIndex]
                : $"TSO {tsoIndex + 1}";

            count += SaveTextures(tso, tsoIndex, category, outputDirectory);
        }

        return count;
    }

    public static int SaveTextures(TSOFile tso, int tsoIndex, string category, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(tso);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var count = 0;
        for (var textureIndex = 0; textureIndex < tso.textures.Length; textureIndex++)
        {
            var texture = tso.textures[textureIndex];
            if (texture.width <= 0 || texture.height <= 0 || texture.data.Length == 0)
            {
                continue;
            }

            using var image = CreateImage(texture);
            var fileName = $"{tsoIndex + 1:00}_{SanitizeFileName(category)}_{textureIndex + 1:00}_{SanitizeFileName(texture.Name)}.bmp";
            image.Save(Path.Combine(outputDirectory, fileName), BmpWithTransparencyEncoder);
            count++;
        }

        return count;
    }

    public static Image<Rgba32> CreateImage(TSOTex texture)
    {
        var image = new Image<Rgba32>(texture.width, texture.height);

        switch (texture.depth)
        {
            case 4:
                using (var source = Image.LoadPixelData<Bgra32>(texture.data, texture.width, texture.height))
                {
                    for (var y = 0; y < texture.height; y++)
                    {
                        for (var x = 0; x < texture.width; x++)
                        {
                            var pixel = source[x, y];
                            image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A);
                        }
                    }
                }
                break;
            case 3:
                using (var source = Image.LoadPixelData<Bgr24>(texture.data, texture.width, texture.height))
                {
                    for (var y = 0; y < texture.height; y++)
                    {
                        for (var x = 0; x < texture.width; x++)
                        {
                            var pixel = source[x, y];
                            image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, byte.MaxValue);
                        }
                    }
                }
                break;
            default:
                image.Dispose();
                throw new NotSupportedException($"Unsupported texture depth: {texture.depth}");
        }

        return image;
    }

    public static void ReplaceTexture(TSOTex texture, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        using var image = Image.Load<Rgba32>(sourcePath);
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidOperationException("Texture image has no pixels.");
        }

        var data = new byte[image.Width * image.Height * 4];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                var offset = ((y * image.Width) + x) * 4;
                data[offset] = pixel.B;
                data[offset + 1] = pixel.G;
                data[offset + 2] = pixel.R;
                data[offset + 3] = pixel.A;
            }
        }

        texture.width = image.Width;
        texture.height = image.Height;
        texture.depth = 4;
        texture.data = data;
        texture.FileName = $"\"{Path.GetFileName(sourcePath)}\"";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Texture" : sanitized;
    }
}
