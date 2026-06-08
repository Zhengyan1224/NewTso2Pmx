using System.Text;

namespace NewTso2Pmx.Core.Loading;

public static class MeshGrouping
{
    public static IReadOnlyList<MeshGroupDescriptor> Build(
        IReadOnlyList<FlatSubMeshInfo> subMeshes,
        MeshGroupingMode mode)
    {
        var groups = new List<MeshGroupDescriptor>();
        var lookup = new Dictionary<string, (StringBuilder Label, int VertexCount, List<int> Indices)>();

        foreach (var subMesh in subMeshes)
        {
            var key = GetKey(subMesh, mode);
            if (!lookup.TryGetValue(key, out var state))
            {
                state = (new StringBuilder(GetLabel(subMesh, mode)), 0, new List<int>());
                lookup.Add(key, state);
            }

            state.VertexCount += subMesh.VertexCount;
            state.Indices.Add(subMesh.FlatIndex);
            lookup[key] = state;
        }

        foreach (var pair in lookup)
        {
            groups.Add(new MeshGroupDescriptor(
                pair.Key,
                pair.Value.Label.ToString(),
                pair.Value.VertexCount,
                pair.Value.Indices));
        }

        return groups;
    }

    private static string GetKey(FlatSubMeshInfo subMesh, MeshGroupingMode mode)
        => mode switch
        {
            MeshGroupingMode.ByMaterial => $"mat:{subMesh.TsoIndex}:{subMesh.ScriptIndex}",
            MeshGroupingMode.ByMesh => $"mesh:{subMesh.TsoIndex}:{subMesh.MeshIndex}",
            MeshGroupingMode.BySubMesh => $"sub:{subMesh.FlatIndex}",
            _ => $"sub:{subMesh.FlatIndex}"
        };

    private static string GetLabel(FlatSubMeshInfo subMesh, MeshGroupingMode mode)
        => mode switch
        {
            MeshGroupingMode.ByMaterial => $"{subMesh.Category} - {subMesh.MaterialName}",
            MeshGroupingMode.ByMesh => $"{subMesh.Category} - {subMesh.MeshName}",
            MeshGroupingMode.BySubMesh => $"{subMesh.Category} - {subMesh.MeshName} - {subMesh.MeshSubIndex} ({subMesh.MaterialName})",
            _ => $"{subMesh.Category} - {subMesh.MeshName}"
        };
}
