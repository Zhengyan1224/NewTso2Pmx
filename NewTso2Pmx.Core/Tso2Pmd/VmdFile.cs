using System.Text;
using NewTso2Pmx.Core.Infrastructure;
using Microsoft.DirectX;

namespace Tso2Pmd;

public sealed class VmdFile
{
    public string Header { get; private set; } = string.Empty;

    public string ModelName { get; private set; } = string.Empty;

    public IReadOnlyList<VmdBoneFrame> BoneFrames { get; private set; } = [];

    public IReadOnlyList<VmdMorphFrame> MorphFrames { get; private set; } = [];

    public uint MaxFrame
    {
        get
        {
            var boneMax = BoneFrames.Count == 0 ? 0 : BoneFrames.Max(frame => frame.FrameNumber);
            var morphMax = MorphFrames.Count == 0 ? 0 : MorphFrames.Max(frame => frame.FrameNumber);
            return Math.Max(boneMax, morphMax);
        }
    }

    public static VmdFile Load(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        using var stream = File.OpenRead(sourcePath);
        return Load(stream);
    }

    public static VmdFile Load(Stream sourceStream)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        using var reader = new BinaryReader(sourceStream, LegacyEncoding.ShiftJis, leaveOpen: true);

        var vmd = new VmdFile
        {
            Header = ReadFixedString(reader, 30),
            ModelName = ReadFixedString(reader, 20)
        };

        if (!vmd.Header.StartsWith("Vocaloid Motion Data", StringComparison.Ordinal))
        {
            throw new InvalidDataException("文件不是有效的 VMD 动作文件。");
        }

        var boneFrames = new List<VmdBoneFrame>();
        var boneFrameCount = reader.ReadUInt32();
        for (var i = 0u; i < boneFrameCount; i++)
        {
            var boneName = ReadFixedString(reader, 15);
            var frameNumber = reader.ReadUInt32();
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var rotation = new Quaternion(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            var interpolation = reader.ReadBytes(64);
            boneFrames.Add(new VmdBoneFrame(boneName, frameNumber, position, rotation, interpolation));
        }

        var morphFrames = new List<VmdMorphFrame>();
        if (sourceStream.Position < sourceStream.Length)
        {
            var morphFrameCount = reader.ReadUInt32();
            for (var i = 0u; i < morphFrameCount; i++)
            {
                morphFrames.Add(new VmdMorphFrame(
                    ReadFixedString(reader, 15),
                    reader.ReadUInt32(),
                    reader.ReadSingle()));
            }
        }

        vmd.BoneFrames = boneFrames;
        vmd.MorphFrames = morphFrames;
        return vmd;
    }

    private static string ReadFixedString(BinaryReader reader, int byteCount)
    {
        var bytes = reader.ReadBytes(byteCount);
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return LegacyEncoding.ShiftJis.GetString(bytes, 0, length);
    }
}

public sealed record VmdBoneFrame(
    string BoneName,
    uint FrameNumber,
    Vector3 Position,
    Quaternion Rotation,
    byte[] Interpolation);

public sealed record VmdMorphFrame(string MorphName, uint FrameNumber, float Weight);
