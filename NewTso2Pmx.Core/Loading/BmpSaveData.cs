using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NewTso2Pmx.Core.Loading;

internal sealed class BmpSaveData
{
    public string[] Names { get; } = new string[32];

    public float[] Proportions { get; } = new float[7];

    public int[] Unknowns { get; } = new int[5];

    public static BmpSaveData Read(Stream stream)
    {
        using var image = Image.Load<Bgra32>(stream);
        var stride = image.Width * 4;
        var data = new byte[stride * image.Height / 8];
        var offset = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = accessor.Height - 1; y >= 0; y--)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += 2)
                {
                    var left = row[x];
                    var right = row[x + 1];
                    byte value = 0;
                    value |= (byte)(left.B & 0x1);
                    value |= (byte)((left.G & 0x1) << 1);
                    value |= (byte)((left.R & 0x1) << 2);
                    value |= (byte)((left.A & 0x1) << 3);
                    value |= (byte)((right.B & 0x1) << 4);
                    value |= (byte)((right.G & 0x1) << 5);
                    value |= (byte)((right.R & 0x1) << 6);
                    value |= (byte)((right.A & 0x1) << 7);
                    data[offset++] = value;
                }
            }
        });

        var result = new BmpSaveData();
        var encoding = Encoding.GetEncoding("Shift_JIS");
        for (var i = 0; i < result.Names.Length; i++)
        {
            result.Names[i] = encoding.GetString(data, i * 32, 32);
        }

        result.Proportions[0] = BitConverter.ToSingle(data, 32 * 32 + 4 * 0);
        result.Proportions[1] = BitConverter.ToSingle(data, 32 * 32 + 4 * 4);
        result.Proportions[2] = BitConverter.ToSingle(data, 32 * 32 + 4 * 5);
        result.Proportions[3] = BitConverter.ToSingle(data, 32 * 32 + 4 * 6);
        result.Proportions[4] = BitConverter.ToSingle(data, 32 * 32 + 4 * 7);
        result.Proportions[5] = BitConverter.ToSingle(data, 32 * 32 + 4 * 8);
        result.Proportions[6] = BitConverter.ToSingle(data, 32 * 32 + 4 * 11);

        result.Unknowns[0] = BitConverter.ToInt32(data, 32 * 32 + 4 * 1);
        result.Unknowns[1] = BitConverter.ToInt32(data, 32 * 32 + 4 * 2);
        result.Unknowns[2] = BitConverter.ToInt32(data, 32 * 32 + 4 * 3);
        result.Unknowns[3] = BitConverter.ToInt32(data, 32 * 32 + 4 * 9);
        result.Unknowns[4] = BitConverter.ToInt32(data, 32 * 32 + 4 * 10);

        return result;
    }
}
