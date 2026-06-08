using System.Text;
using TDCG;

namespace NewTso2Pmx.Core.Loading;

public static class TdcgPngExporter
{
    private static readonly byte[] PngHeader = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] OnePixelRgbaPngIhdr =
    [
        0x00, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x01,
        0x08,
        0x06,
        0x00,
        0x00,
        0x00
    ];

    private static readonly byte[] OnePixelTransparentIdat =
    [
        0x78, 0x9C, 0x63, 0x60, 0x00, 0x02, 0x00, 0x00, 0x05, 0x00, 0x01
    ];

    private static readonly string[] CategoryNames =
    [
        "身体",
        "前髪",
        "後髪",
        "頭皮",
        "瞳",
        "ブラ",
        "全身下着・水着",
        "パンツ",
        "靴下",
        "上衣",
        "全身衣装",
        "上着オプション",
        "下衣",
        "尻尾",
        "靴",
        "頭部装備",
        "眼鏡",
        "首輪",
        "手首",
        "背中",
        "アホ毛類",
        "眼帯",
        "タイツ・ガーター",
        "腕装備",
        "リボン",
        "手持ちの小物or背景",
        "眉毛",
        "ほくろ",
        "八重歯",
        "イヤリング類"
    ];

    public static IReadOnlyList<string> Categories => CategoryNames;

    public static void Save(LoadedDocument document, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        using var stream = File.Create(destinationPath);
        Save(document, stream);
    }

    public static void Save(LoadedDocument document, Stream destinationStream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destinationStream);

        var png = new PNGFile
        {
            WriteTaOb = writer =>
            {
                var pngWriter = new PNGWriter(writer);
                pngWriter.WriteTDCG();
                pngWriter.WriteSCNE(1);
                pngWriter.WriteFTMO(document.Figure.Tmo);
                pngWriter.WriteFIGU(CreateFiguData(document.Figure.slider_matrix));

                for (var index = 0; index < document.Figure.TSOList.Count; index++)
                {
                    using var tsoStream = new MemoryStream();
                    document.Figure.TSOList[index].Save(tsoStream);
                    tsoStream.Position = 0;

                    var category = document.Categories.Count > index
                        ? document.Categories[index]
                        : string.Empty;
                    pngWriter.WriteFTSO(GetCategoryIndex(category), tsoStream);
                }
            }
        };

        png.header = PngHeader;
        png.ihdr = OnePixelRgbaPngIhdr;
        png.IdatList.Add(OnePixelTransparentIdat);
        png.Save(destinationStream);
    }

    private static byte[] CreateFiguData(SliderMatrix sliderMatrix)
    {
        using var stream = new MemoryStream(sizeof(float) * 7);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(sliderMatrix.TallRatio);
        writer.Write(sliderMatrix.ArmRatio);
        writer.Write(sliderMatrix.LegRatio);
        writer.Write(sliderMatrix.WaistRatio);
        writer.Write(sliderMatrix.BustRatio);
        writer.Write(sliderMatrix.EyeRatio);
        writer.Write(0.0f);
        writer.Flush();

        return stream.ToArray();
    }

    private static uint GetCategoryIndex(string category)
    {
        var index = Array.FindIndex(CategoryNames, item => string.Equals(item, category, StringComparison.Ordinal));
        return index >= 0 ? (uint)index : 0;
    }
}
