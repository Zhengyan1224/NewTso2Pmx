using System.Globalization;
using NewTso2Pmx.Core.Infrastructure;
using TDCG;
using Microsoft.DirectX;

namespace Tso2Pmd;

public static class VpdExporter
{
    private const string CenterBoneName = "センター";
    private const string LowerBodyBoneName = "下半身";
    private const string HeadBoneName = "頭";
    private const string PmdInitPoseName = "TDCG.Proportion.AAA_PMDInitPose";

    public static void Save(Figure figure, string correspondTableDirectory, Stream destinationStream)
    {
        ArgumentNullException.ThrowIfNull(figure);

        var initialPose = CreateInitialPose(figure);
        Save(figure, initialPose, correspondTableDirectory, destinationStream);
    }

    public static void Save(
        Figure figure,
        TMOFile initialPose,
        string correspondTableDirectory,
        Stream destinationStream)
    {
        ArgumentNullException.ThrowIfNull(figure);
        ArgumentNullException.ThrowIfNull(initialPose);
        ArgumentException.ThrowIfNullOrWhiteSpace(correspondTableDirectory);
        ArgumentNullException.ThrowIfNull(destinationStream);

        var mappings = VpdBoneMapping.LoadFromCorrespondTable(correspondTableDirectory);
        if (mappings.Count == 0)
        {
            throw new InvalidDataException("骨骼对应表中没有可用于 VPD 导出的骨骼映射。");
        }

        var initPose = PreparePose(initialPose, "初始姿势");
        var pose = PreparePose(figure.Tmo, "当前姿势");
        var pmdInitPose = CreatePmdInitialPose(initPose);

        var pmdInitPoseDiff = DiffTmo(pmdInitPose, initPose);
        var pmdInitPoseDiffW = DiffWorldTmo(pmdInitPose, initPose);
        var poseDiff = DiffTmo(pose, initPose);
        var output = MultiTmo(UnitaryTmo(poseDiff, pmdInitPoseDiffW), InvertTmo(pmdInitPoseDiff));
        var output2 = UnitaryTmo(poseDiff, pmdInitPoseDiffW);

        using var writer = new StreamWriter(
            destinationStream,
            LegacyEncoding.ShiftJis,
            bufferSize: 1024,
            leaveOpen: true);

        writer.WriteLine("Vocaloid Pose Data file");
        writer.WriteLine();
        writer.WriteLine("miku.osm;");
        writer.WriteLine(FormattableString.Invariant($"{mappings.Count};"));

        for (var i = 0; i < mappings.Count; i++)
        {
            var mapping = mappings[i];
            var rotation = GetVpdRotation(mapping, output, output2);

            writer.WriteLine();
            writer.WriteLine(FormattableString.Invariant($"Bone{i}{{{mapping.TargetBoneName}"));
            writer.WriteLine("0.000000,0.000000,0.000000;");
            writer.WriteLine(FormatRotation(rotation));
            writer.WriteLine("}");
        }
    }

    private static TMOFile CreateInitialPose(Figure figure)
    {
        if (figure.TSOList.Count > 0)
        {
            return figure.TSOList[0].GenerateTMO();
        }

        return figure.Tmo.Dup();
    }

    private static TMOFile CreatePmdInitialPose(TMOFile initialPose)
    {
        var pmdFigure = new Figure
        {
            Tmo = initialPose.Dup()
        };

        var pmdInitPose = pmdFigure.TPOList[PmdInitPoseName];
        if (pmdInitPose is not null)
        {
            pmdInitPose.Ratio = 1.0f;
        }

        pmdFigure.TransformTpo(0);
        var result = PreparePose(pmdFigure.Tmo, "PMD 初始姿势");
        pmdFigure.Dispose();
        return result;
    }

    private static TMOFile PreparePose(TMOFile source, string label)
    {
        if (source.nodes is null || source.frames is null || source.frames.Length == 0)
        {
            throw new InvalidDataException($"{label} TMO 不包含可导出的骨骼帧。");
        }

        var pose = source.Dup();
        pose.LoadTransformationMatrixFromFrame(0);
        return pose;
    }

    private static TMOFile DiffWorldTmo(TMOFile tmo1, TMOFile tmo2)
    {
        var output = tmo1.Dup();
        output.LoadTransformationMatrixFromFrame(0);

        foreach (var node in tmo1.nodes)
        {
            var outputNode = FindRequiredNode(output, node.Name);
            var node2 = FindRequiredNode(tmo2, node.Name);
            outputNode.Rotation = Quaternion.Invert(GetWorldRotation(node2)) * GetWorldRotation(node);
        }

        return output;
    }

    private static TMOFile DiffTmo(TMOFile tmo1, TMOFile tmo2)
        => MultiTmo(InvertTmo(tmo2), tmo1);

    private static TMOFile UnitaryTmo(TMOFile tmo1, TMOFile tmo2)
        => MultiTmo(MultiTmo(InvertTmo(tmo2), tmo1), tmo2);

    private static TMOFile MultiTmo(TMOFile tmo1, TMOFile tmo2)
    {
        var output = tmo1.Dup();
        output.LoadTransformationMatrixFromFrame(0);

        foreach (var node in tmo1.nodes)
        {
            var outputNode = FindRequiredNode(output, node.Name);
            var node2 = FindRequiredNode(tmo2, node.Name);
            outputNode.Rotation = node.Rotation * node2.Rotation;
        }

        return output;
    }

    private static TMOFile InvertTmo(TMOFile tmo)
    {
        var output = tmo.Dup();
        output.LoadTransformationMatrixFromFrame(0);

        foreach (var node in tmo.nodes)
        {
            var outputNode = FindRequiredNode(output, node.Name);
            outputNode.Rotation = Quaternion.Invert(node.Rotation);
        }

        return output;
    }

    private static Quaternion GetVpdRotation(VpdBoneMapping mapping, TMOFile output, TMOFile output2)
    {
        if (mapping.TargetBoneName == CenterBoneName)
        {
            return FindRequiredNode(output, "W_Hips").Rotation *
                   FindRequiredNode(output, "W_Spine_Dummy").Rotation;
        }

        if (mapping.TargetBoneName == LowerBodyBoneName)
        {
            return Quaternion.Invert(FindRequiredNode(output, "W_Spine_Dummy").Rotation);
        }

        if (mapping.TargetBoneName == HeadBoneName)
        {
            var faceRotation = TryFindNode(output, "face_oya")?.Rotation ?? Quaternion.Identity;
            return FindRequiredNode(output, "Head").Rotation *
                   new Quaternion(-faceRotation.X, faceRotation.Y, -faceRotation.Z, faceRotation.W);
        }

        var sourceTmo = mapping.TargetBoneName.Contains('捩', StringComparison.Ordinal)
            ? output2
            : output;

        var rotation = Quaternion.Identity;
        foreach (var sourceBoneName in mapping.SourceBoneNames)
        {
            var node = TryFindNode(sourceTmo, sourceBoneName);
            if (node is not null)
            {
                rotation *= node.Rotation;
            }
        }

        return rotation;
    }

    private static TMONode FindRequiredNode(TMOFile tmo, string nodeName)
        => TryFindNode(tmo, nodeName)
           ?? throw new InvalidDataException($"TMO 缺少 VPD 导出所需骨骼：{nodeName}");

    private static TMONode? TryFindNode(TMOFile tmo, string nodeName)
        => tmo.FindNodeByName(nodeName);

    private static Quaternion GetWorldRotation(TMONode node)
    {
        var world = node.GetWorldCoordinate();
        world.M41 = 0.0f;
        world.M42 = 0.0f;
        world.M43 = 0.0f;
        return Quaternion.RotationMatrix(world);
    }

    private static string FormatRotation(Quaternion rotation)
        => string.Create(CultureInfo.InvariantCulture,
            $"{-rotation.X},{-rotation.Y},{rotation.Z},{rotation.W};");

    private sealed record VpdBoneMapping(string TargetBoneName, IReadOnlyList<string> SourceBoneNames)
    {
        public static IReadOnlyList<VpdBoneMapping> LoadFromCorrespondTable(string tableDirectory)
        {
            var skinningPath = Path.Combine(tableDirectory, "skinning.txt");
            var boneStructurePath = Path.Combine(tableDirectory, "boneStructure.txt");
            if (!File.Exists(skinningPath))
            {
                throw new FileNotFoundException("找不到 VPD 导出所需的 skinning.txt。", skinningPath);
            }

            var groups = ReadSkinningGroups(skinningPath);
            var order = ReadBoneOrder(boneStructurePath)
                .Where(name => name == CenterBoneName || groups.ContainsKey(name))
                .ToList();

            if (!order.Contains(CenterBoneName, StringComparer.Ordinal))
            {
                order.Insert(0, CenterBoneName);
            }

            foreach (var name in groups.Keys)
            {
                if (!order.Contains(name, StringComparer.Ordinal))
                {
                    order.Add(name);
                }
            }

            return order
                .Select(name => new VpdBoneMapping(
                    name,
                    groups.TryGetValue(name, out var sourceBones) ? sourceBones : []))
                .ToList();
        }

        private static Dictionary<string, List<string>> ReadSkinningGroups(string path)
        {
            var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var line in File.ReadLines(path, LegacyEncoding.ShiftJis))
            {
                var columns = SplitCsvLine(line);
                if (columns.Length < 2)
                {
                    continue;
                }

                var sourceBone = columns[0].Trim();
                var targetBone = columns[1].Trim();
                if (string.IsNullOrWhiteSpace(sourceBone) || string.IsNullOrWhiteSpace(targetBone))
                {
                    continue;
                }

                if (!groups.TryGetValue(targetBone, out var sourceBones))
                {
                    sourceBones = [];
                    groups[targetBone] = sourceBones;
                }

                if (!sourceBones.Contains(sourceBone, StringComparer.Ordinal))
                {
                    sourceBones.Add(sourceBone);
                }
            }

            return groups;
        }

        private static IEnumerable<string> ReadBoneOrder(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(path, LegacyEncoding.ShiftJis))
            {
                var columns = SplitCsvLine(line);
                if (columns.Length == 0)
                {
                    continue;
                }

                var boneName = columns[0].Trim();
                if (!string.IsNullOrWhiteSpace(boneName))
                {
                    yield return boneName;
                }
            }
        }

        private static string[] SplitCsvLine(string line)
            => line.Split(',', StringSplitOptions.TrimEntries);
    }
}
