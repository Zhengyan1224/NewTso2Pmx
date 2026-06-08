using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NewTso2Pmx.Core.Infrastructure;

public static class LegacyEncoding
{
    private static readonly UTF8Encoding Utf8Strict = new(false, true);
    private static readonly UTF8Encoding Utf8Bom = new(true, true);
    private static readonly UnicodeEncoding Utf16LeBom = new(false, true, true);
    private static readonly UnicodeEncoding Utf16BeBom = new(true, true, true);

    public static Encoding ShiftJis { get; } = Encoding.GetEncoding(932);

    public static BinaryReader CreateShiftJisBinaryReader(Stream stream, bool leaveOpen = false)
        => new(stream, ShiftJis, leaveOpen);

    public static BinaryWriter CreateShiftJisBinaryWriter(Stream stream, bool leaveOpen = false)
        => new(stream, ShiftJis, leaveOpen);

    public static (string Text, Encoding Encoding) ReadAllTextWithDetection(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return DecodeText(bytes);
    }

    public static string[] ReadAllLinesWithDetection(string path)
    {
        string text = ReadAllTextWithDetection(path).Text;
        List<string> lines = new();

        using StringReader reader = new(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    public static void WriteAllLinesShiftJis(string path, IEnumerable<string> lines)
    {
        using StreamWriter writer = new(path, false, ShiftJis);
        foreach (string line in lines)
        {
            writer.WriteLine(line);
        }
    }

    private static (string Text, Encoding Encoding) DecodeText(byte[] bytes)
    {
        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return (Utf8Bom.GetString(bytes), Utf8Bom);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return (Utf16LeBom.GetString(bytes), Utf16LeBom);
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return (Utf16BeBom.GetString(bytes), Utf16BeBom);
        }

        try
        {
            return (Utf8Strict.GetString(bytes), Utf8Strict);
        }
        catch (DecoderFallbackException)
        {
            return (ShiftJis.GetString(bytes), ShiftJis);
        }
    }

    private static bool HasPrefix(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (int i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }
}
