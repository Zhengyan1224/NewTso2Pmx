using System.Numerics;

namespace NewTso2Pmx.Core.Preview;

public sealed class PreviewSceneData
{
    public static PreviewSceneData Empty { get; } = new([], [], [], Vector3.Zero, Vector3.Zero, Vector3.Zero);

    public PreviewSceneData(
        IReadOnlyList<PreviewVertex> vertices,
        IReadOnlyList<uint> indices,
        IReadOnlyList<PreviewDrawBatch> drawBatches,
        Vector3 center,
        Vector3 min,
        Vector3 max)
    {
        Vertices = vertices;
        Indices = indices;
        DrawBatches = drawBatches;
        Center = center;
        Min = min;
        Max = max;
    }

    public IReadOnlyList<PreviewVertex> Vertices { get; }

    public IReadOnlyList<uint> Indices { get; }

    public IReadOnlyList<PreviewDrawBatch> DrawBatches { get; }

    public Vector3 Center { get; }

    public Vector3 Min { get; }

    public Vector3 Max { get; }

    public bool IsEmpty => Vertices.Count == 0 || Indices.Count == 0;
}

public sealed record PreviewDrawBatch(
    int StartIndex,
    int IndexCount,
    PreviewTextureData? ColorTexture);

public sealed record PreviewTextureData(
    string Name,
    int Width,
    int Height,
    byte[] RgbaPixels);
