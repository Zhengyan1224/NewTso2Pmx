using NewTso2Pmx.Core.Infrastructure;
using Microsoft.DirectX;
using TDCG;

namespace Tso2Pmd;

public sealed class VmdTmoConverter
{
    private const string CenterBoneName = "センター";
    private const string LowerBodyBoneName = "下半身";
    private const string PmdInitPoseName = "TDCG.Proportion.AAA_PMDInitPose";

    private readonly IReadOnlyDictionary<string, string> _mmdToTdcgBoneNames;

    public VmdTmoConverter(string correspondTableDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correspondTableDirectory);
        _mmdToTdcgBoneNames = LoadBoneMapping(correspondTableDirectory);
    }

    public TMOFile Convert(Figure figure, VmdFile vmd, Morphing? morphing = null)
    {
        ArgumentNullException.ThrowIfNull(figure);
        ArgumentNullException.ThrowIfNull(vmd);

        var initPose = CreateInitialPose(figure);
        initPose.LoadTransformationMatrixFromFrame(0);
        var rotationAdapter = new RotationAdapter(initPose);
        var sourceFrameCount = Math.Max(1, (int)vmd.MaxFrame + 1);
        var output = initPose.Dup();
        output.frames = new TMOFrame[sourceFrameCount];
        output.opt0 = sourceFrameCount - 1;

        var boneTracks = vmd.BoneFrames
            .GroupBy(frame => frame.BoneName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(frame => frame.FrameNumber).ToArray(),
                StringComparer.Ordinal);
        var morphTracks = vmd.MorphFrames
            .GroupBy(frame => frame.MorphName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(frame => frame.FrameNumber).ToArray(),
                StringComparer.Ordinal);

        for (var frameIndex = 0; frameIndex < sourceFrameCount; frameIndex++)
        {
            initPose.LoadTransformationMatrixFromFrame(0);
            ApplyMorphFrame(initPose, morphing, morphTracks, frameIndex);
            ApplyBoneFrame(initPose, rotationAdapter, boneTracks, frameIndex);
            output.frames[frameIndex] = CreateFrameFromTransformations(output.nodes, initPose.nodes, frameIndex);
        }

        foreach (var node in output.nodes)
        {
            node.LinkMatrices(output.frames);
        }

        output.LoadTransformationMatrixFromFrame(0);
        return output;
    }

    private static TMOFile CreateInitialPose(Figure figure)
    {
        if (figure.TSOList.Count > 0)
        {
            return figure.TSOList[0].GenerateTMO();
        }

        return figure.Tmo.Dup();
    }

    private void ApplyBoneFrame(
        TMOFile tmo,
        RotationAdapter rotationAdapter,
        IReadOnlyDictionary<string, VmdBoneFrame[]> boneTracks,
        int frameIndex)
    {
        var centerFrame = SampleBoneFrame(boneTracks, CenterBoneName, frameIndex);
        var lowerBodyFrame = SampleBoneFrame(boneTracks, LowerBodyBoneName, frameIndex);

        var hipsNode = FindNode(tmo, "W_Hips");
        var spineDummyNode = FindNode(tmo, "W_Spine_Dummy");
        if (hipsNode is not null && spineDummyNode is not null)
        {
            var centerRotation = rotationAdapter.MmdRotationToCustomRotation(ToCustomQuaternion(centerFrame.Rotation), "W_Hips");
            var lowerBodyRotation = rotationAdapter.MmdRotationToCustomRotation(ToCustomQuaternion(lowerBodyFrame.Rotation), "W_Spine_Dummy");

            hipsNode.Rotation = lowerBodyRotation * centerRotation;
            spineDummyNode.Rotation = Quaternion.Invert(lowerBodyRotation);

            var spine1Node = FindNode(tmo, "W_Spine1");
            var position = ToCustomPosition(centerFrame.Position);
            if (spine1Node is not null)
            {
                var delta = spine1Node.GetWorldPosition() - hipsNode.GetWorldPosition();
                hipsNode.Translation = position + tmo.nodes[0].Translation - delta;
            }
            else
            {
                hipsNode.Translation = position + tmo.nodes[0].Translation;
            }
        }

        foreach (var pair in _mmdToTdcgBoneNames)
        {
            if (pair.Key == CenterBoneName || pair.Key == LowerBodyBoneName)
            {
                continue;
            }

            var node = FindNode(tmo, pair.Value);
            if (node is null)
            {
                continue;
            }

            var frame = SampleBoneFrame(boneTracks, pair.Key, frameIndex);
            node.Rotation = rotationAdapter.MmdRotationToCustomRotation(ToCustomQuaternion(frame.Rotation), pair.Value);
        }
    }

    private static TMOFrame CreateFrameFromTransformations(TMONode[] outputNodes, TMONode[] sourceNodes, int frameIndex)
    {
        var frame = new TMOFrame(frameIndex)
        {
            matrices = new TMOMat[outputNodes.Length]
        };

        for (var i = 0; i < outputNodes.Length; i++)
        {
            var sourceNode = sourceNodes[i];
            var matrix = sourceNode.TransformationMatrix;
            frame.matrices[i] = new TMOMat(ref matrix);
        }

        return frame;
    }

    private static VmdBoneFrame SampleBoneFrame(
        IReadOnlyDictionary<string, VmdBoneFrame[]> boneTracks,
        string boneName,
        int frameIndex)
    {
        if (!boneTracks.TryGetValue(boneName, out var frames) || frames.Length == 0)
        {
            return new VmdBoneFrame(boneName, (uint)frameIndex, Vector3.Empty, Quaternion.Identity, []);
        }

        if (frameIndex <= frames[0].FrameNumber)
        {
            return frames[0] with { FrameNumber = (uint)frameIndex };
        }

        var last = frames[^1];
        if (frameIndex >= last.FrameNumber)
        {
            return last with { FrameNumber = (uint)frameIndex };
        }

        for (var i = 1; i < frames.Length; i++)
        {
            var next = frames[i];
            if (frameIndex > next.FrameNumber)
            {
                continue;
            }

            var previous = frames[i - 1];
            var amount = (frameIndex - previous.FrameNumber) / (float)(next.FrameNumber - previous.FrameNumber);
            var interpolation = new VmdInterpolation(next.Interpolation);
            return new VmdBoneFrame(
                boneName,
                (uint)frameIndex,
                new Vector3(
                    Lerp(previous.Position.X, next.Position.X, interpolation.X.Evaluate(amount)),
                    Lerp(previous.Position.Y, next.Position.Y, interpolation.Y.Evaluate(amount)),
                    Lerp(previous.Position.Z, next.Position.Z, interpolation.Z.Evaluate(amount))),
                Quaternion.Slerp(previous.Rotation, next.Rotation, interpolation.Rotation.Evaluate(amount)),
                next.Interpolation);
        }

        return last with { FrameNumber = (uint)frameIndex };
    }

    private static float Lerp(float previous, float next, float amount)
        => previous + ((next - previous) * amount);

    private static void ApplyMorphFrame(
        TMOFile tmo,
        Morphing? morphing,
        IReadOnlyDictionary<string, VmdMorphFrame[]> morphTracks,
        int frameIndex)
    {
        if (morphing is null || morphTracks.Count == 0)
        {
            return;
        }

        foreach (var group in morphing.Groups)
        {
            foreach (var morph in group.Items)
            {
                morph.Ratio = SampleMorphWeight(morphTracks, morph.Name, frameIndex);
            }
        }

        morphing.Morph(tmo);
    }

    private static float SampleMorphWeight(
        IReadOnlyDictionary<string, VmdMorphFrame[]> morphTracks,
        string morphName,
        int frameIndex)
    {
        if (!morphTracks.TryGetValue(morphName, out var frames) || frames.Length == 0)
        {
            return 0.0f;
        }

        if (frameIndex <= frames[0].FrameNumber)
        {
            return frames[0].Weight;
        }

        var last = frames[^1];
        if (frameIndex >= last.FrameNumber)
        {
            return last.Weight;
        }

        for (var i = 1; i < frames.Length; i++)
        {
            var next = frames[i];
            if (frameIndex > next.FrameNumber)
            {
                continue;
            }

            var previous = frames[i - 1];
            var amount = (frameIndex - previous.FrameNumber) / (float)(next.FrameNumber - previous.FrameNumber);
            return previous.Weight + ((next.Weight - previous.Weight) * amount);
        }

        return last.Weight;
    }

    private static Quaternion ToCustomQuaternion(Quaternion rotation)
        => new(-rotation.X, -rotation.Y, rotation.Z, rotation.W);

    private static Vector3 ToCustomPosition(Vector3 position)
        => new(position.X, position.Y, -position.Z);

    private static TMONode? FindNode(TMOFile tmo, string nodeName)
        => tmo.FindNodeByName(nodeName);

    private static IReadOnlyDictionary<string, string> LoadBoneMapping(string tableDirectory)
    {
        var path = Path.Combine(tableDirectory, "bonePosition.txt");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("找不到 VMD 转 TMO 所需的 bonePosition.txt。", path);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path, LegacyEncoding.ShiftJis))
        {
            var columns = line.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length < 2)
            {
                continue;
            }

            var tdcgBoneName = columns[0].Trim();
            var mmdBoneName = columns[1].Trim();
            if (string.IsNullOrWhiteSpace(tdcgBoneName) || string.IsNullOrWhiteSpace(mmdBoneName))
            {
                continue;
            }

            map[mmdBoneName] = tdcgBoneName;
        }

        return map;
    }

    private sealed class RotationAdapter
    {
        private readonly TMOFile _initPose;
        private readonly TMOFile _pmdInitPoseDiff;
        private readonly TMOFile _pmdInitPoseDiffWorld;

        public RotationAdapter(TMOFile initialPose)
        {
            _initPose = initialPose.Dup();
            _initPose.LoadTransformationMatrixFromFrame(0);

            using var pmdFigure = new Figure
            {
                Tmo = initialPose.Dup()
            };

            var pmdInitPose = pmdFigure.TPOList[PmdInitPoseName];
            if (pmdInitPose is not null)
            {
                pmdInitPose.Ratio = 1.0f;
            }

            pmdFigure.TransformTpo(0);
            pmdFigure.Tmo.LoadTransformationMatrixFromFrame(0);

            _pmdInitPoseDiff = DiffTmo(pmdFigure.Tmo, _initPose);
            _pmdInitPoseDiffWorld = DiffWorldTmo(pmdFigure.Tmo, _initPose);
        }

        public Quaternion MmdRotationToCustomRotation(Quaternion rotation, string customBoneName)
        {
            var initNode = FindRequiredNode(_initPose, customBoneName);
            var diffWorldNode = FindRequiredNode(_pmdInitPoseDiffWorld, customBoneName);
            var diffNode = FindRequiredNode(_pmdInitPoseDiff, customBoneName);

            return initNode.Rotation *
                   diffWorldNode.Rotation *
                   rotation *
                   diffNode.Rotation *
                   Quaternion.Invert(diffWorldNode.Rotation);
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

        private static TMONode FindRequiredNode(TMOFile tmo, string nodeName)
            => tmo.FindNodeByName(nodeName)
               ?? throw new InvalidDataException($"TMO 缺少 VMD 转换所需骨骼：{nodeName}");

        private static Quaternion GetWorldRotation(TMONode node)
        {
            var current = node;
            var rotation = Quaternion.Identity;
            while (current is not null)
            {
                rotation *= current.Rotation;
                current = current.parent;
            }

            return rotation;
        }
    }
}
