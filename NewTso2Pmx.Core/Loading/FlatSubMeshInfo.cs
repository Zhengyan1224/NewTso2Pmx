using TDCG;

namespace NewTso2Pmx.Core.Loading;

public sealed record FlatSubMeshInfo(
    int FlatIndex,
    int TsoIndex,
    int ScriptIndex,
    int MeshIndex,
    int MeshSubIndex,
    string Category,
    string MaterialName,
    string MeshName,
    int VertexCount,
    TSOSubMesh SubMesh);
