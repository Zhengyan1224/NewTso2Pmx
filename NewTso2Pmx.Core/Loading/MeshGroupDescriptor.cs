namespace NewTso2Pmx.Core.Loading;

public sealed record MeshGroupDescriptor(
    string Key,
    string Label,
    int VertexCount,
    IReadOnlyList<int> FlatSubMeshIndices);
