using Microsoft.DirectX;
using TDCG;
using NewTso2Pmx.Core.Loading;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsVector4 = System.Numerics.Vector4;

namespace NewTso2Pmx.Core.Preview;

public sealed class PreviewSceneBuilder
{
    private static readonly NumericsVector4[] Palette =
    [
        new(0.85f, 0.27f, 0.19f, 1.0f),
        new(0.10f, 0.58f, 0.76f, 1.0f),
        new(0.18f, 0.66f, 0.40f, 1.0f),
        new(0.93f, 0.69f, 0.13f, 1.0f),
        new(0.56f, 0.39f, 0.77f, 1.0f),
        new(0.90f, 0.49f, 0.13f, 1.0f)
    ];

    private static readonly NumericsVector3 DefaultLightDirection = NumericsVector3.Normalize(new(0.3f, 0.8f, 0.6f));

    public PreviewSceneData Build(LoadedDocument document, IReadOnlyList<bool> selectedSubMeshes)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(selectedSubMeshes);

        if (selectedSubMeshes.Count != document.SubMeshes.Count)
        {
            throw new ArgumentException("网格选择数量与文档子网格数量不一致。", nameof(selectedSubMeshes));
        }

        var vertices = new List<PreviewVertex>();
        var indices = new List<uint>();
        var drawBatches = new List<PreviewDrawBatch>();
        var min = new NumericsVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new NumericsVector3(float.MinValue, float.MinValue, float.MinValue);

        for (var tsoIndex = 0; tsoIndex < document.Figure.TSOList.Count; tsoIndex++)
        {
            var tso = document.Figure.TSOList[tsoIndex];
            for (var scriptIndex = 0; scriptIndex < tso.sub_scripts.Length; scriptIndex++)
            {
                var color = Palette[(tsoIndex + scriptIndex) % Palette.Length];
                var batchStart = indices.Count;
                foreach (var mesh in tso.meshes)
                {
                    foreach (var subMesh in mesh.sub_meshes)
                    {
                        var info = document.SubMeshes.FirstOrDefault(item =>
                            item.TsoIndex == tsoIndex &&
                            item.ScriptIndex == scriptIndex &&
                            ReferenceEquals(item.SubMesh, subMesh));

                        if (info is null || !selectedSubMeshes[info.FlatIndex] || subMesh.spec != scriptIndex)
                        {
                            continue;
                        }

                        AppendSubMesh(document.Figure, subMesh, color, vertices, indices, ref min, ref max);
                    }
                }

                var batchIndexCount = indices.Count - batchStart;
                if (batchIndexCount > 0)
                {
                    var subScript = tso.sub_scripts[scriptIndex];
                    drawBatches.Add(new PreviewDrawBatch(
                        batchStart,
                        batchIndexCount,
                        CreatePreviewTexture(tso, GetColorTextureName(subScript)),
                        CreatePreviewTexture(tso, GetShadeTextureName(subScript)),
                        GetLightDirection(subScript),
                        GetOutlineColor(subScript),
                        GetOutlineThickness(subScript)));
                }
            }
        }

        if (vertices.Count == 0 || indices.Count == 0)
        {
            return PreviewSceneData.Empty;
        }

        var center = (min + max) * 0.5f;
        return new PreviewSceneData(vertices, indices, drawBatches, center, min, max);
    }

    private static void AppendSubMesh(
        Figure figure,
        TSOSubMesh subMesh,
        NumericsVector4 color,
        ICollection<PreviewVertex> vertices,
        ICollection<uint> indices,
        ref NumericsVector3 min,
        ref NumericsVector3 max)
    {
        var baseIndex = (uint)vertices.Count;
        var clippedBoneMatrices = ClipBoneMatrices(subMesh, figure.Tmo);
        var localVertices = new List<PreviewVertex>(subMesh.vertices.Length);

        foreach (var vertex in subMesh.vertices)
        {
            var position = Microsoft.DirectX.Vector3.Empty;
            var normal = Microsoft.DirectX.Vector3.Empty;

            foreach (var skinWeight in vertex.skin_weights)
            {
                var transform = clippedBoneMatrices[skinWeight.bone_index];
                position += Microsoft.DirectX.Vector3.TransformCoordinate(vertex.position, transform) * skinWeight.weight;

                transform.M41 = 0;
                transform.M42 = 0;
                transform.M43 = 0;
                normal += Microsoft.DirectX.Vector3.TransformCoordinate(vertex.normal, transform) * skinWeight.weight;
            }

            var normalized = Microsoft.DirectX.Vector3.Normalize(normal);
            var previewVertex = new PreviewVertex(
                new NumericsVector3(position.X, position.Y, position.Z),
                new NumericsVector3(normalized.X, normalized.Y, normalized.Z),
                color,
                new System.Numerics.Vector2(vertex.u, 1.0f - vertex.v));

            localVertices.Add(previewVertex);
            vertices.Add(previewVertex);

            min = NumericsVector3.Min(min, previewVertex.Position);
            max = NumericsVector3.Max(max, previewVertex.Position);
        }

        var a = uint.MaxValue;
        var b = uint.MaxValue;
        var c = uint.MaxValue;

        for (var i = 0; i < localVertices.Count; i++)
        {
            a = b;
            b = c;
            c = baseIndex + (uint)i;

            if (i < 2)
            {
                continue;
            }

            var va = localVertices[(int)(a - baseIndex)];
            var vb = localVertices[(int)(b - baseIndex)];
            var vc = localVertices[(int)(c - baseIndex)];

            if (va.Position == vb.Position || vb.Position == vc.Position || vc.Position == va.Position)
            {
                continue;
            }

            if (i % 2 == 0)
            {
                indices.Add(c);
                indices.Add(b);
                indices.Add(a);
            }
            else
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
            }
        }
    }

    private static Matrix[] ClipBoneMatrices(TSOSubMesh subMesh, TMOFile tmo)
    {
        var clippedBoneMatrices = new Matrix[subMesh.maxPalettes];
        for (var paletteIndex = 0; paletteIndex < subMesh.maxPalettes; paletteIndex++)
        {
            var tsoNode = subMesh.GetBone(paletteIndex);
            var tmoNode = tmo.FindNodeByName(tsoNode.Name);
            clippedBoneMatrices[paletteIndex] = tsoNode.offset_matrix * tmoNode.combined_matrix;
        }

        return clippedBoneMatrices;
    }

    private static string? GetColorTextureName(TSOSubScript subScript)
    {
        try
        {
            return subScript.shader.ColorTexName;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetShadeTextureName(TSOSubScript subScript)
    {
        try
        {
            return subScript.shader.ShadeTexName;
        }
        catch
        {
            return null;
        }
    }

    private static NumericsVector3 GetLightDirection(TSOSubScript subScript)
    {
        try
        {
            var lightDir = subScript.shader.LightDir;
            var direction = new NumericsVector3(-lightDir.X, -lightDir.Y, -lightDir.Z);
            if (direction.LengthSquared() < 0.000001f)
            {
                return DefaultLightDirection;
            }

            return NumericsVector3.Normalize(direction);
        }
        catch
        {
            return DefaultLightDirection;
        }
    }

    private static NumericsVector4 GetOutlineColor(TSOSubScript subScript)
    {
        if (!UsesOutline(subScript))
        {
            return NumericsVector4.Zero;
        }

        var parameter = GetShaderParameter(subScript, "PenColor");
        if (parameter is null)
        {
            return new NumericsVector4(0.0f, 0.0f, 0.0f, 1.0f);
        }

        var color = parameter.GetFloat4();
        return new NumericsVector4(color.X, color.Y, color.Z, color.W);
    }

    private static float GetOutlineThickness(TSOSubScript subScript)
    {
        if (!UsesOutline(subScript))
        {
            return 0.0f;
        }

        var thickness = GetShaderParameter(subScript, "Thickness")?.GetFloat() ?? 0.001f;
        return Math.Clamp(thickness, 0.0f, 1.0f);
    }

    private static bool UsesOutline(TSOSubScript subScript)
    {
        var technique = GetTechniqueName(subScript);
        return !string.IsNullOrWhiteSpace(technique) &&
            technique.Contains("Shadow", StringComparison.OrdinalIgnoreCase) &&
            !technique.Contains("InkOff", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetTechniqueName(TSOSubScript subScript)
    {
        return GetShaderParameter(subScript, "technique")?.GetString();
    }

    private static ShaderParameter? GetShaderParameter(TSOSubScript subScript, string name)
    {
        try
        {
            return subScript.shader.shader_parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, name, StringComparison.Ordinal));
        }
        catch
        {
            return null;
        }
    }

    private static PreviewTextureData? CreatePreviewTexture(TSOFile tso, string? textureName)
    {
        if (string.IsNullOrWhiteSpace(textureName))
        {
            return null;
        }

        var texture = tso.textures.FirstOrDefault(item =>
            string.Equals(item.Name, textureName, StringComparison.Ordinal));
        if (texture is null ||
            texture.width <= 0 ||
            texture.height <= 0 ||
            texture.depth < 3 ||
            texture.data.Length < texture.width * texture.height * texture.depth)
        {
            return null;
        }

        var rgba = new byte[texture.width * texture.height * 4];
        var destination = 0;
        for (var y = texture.height - 1; y >= 0; y--)
        {
            var source = y * texture.width * texture.depth;
            for (var x = 0; x < texture.width; x++)
            {
                rgba[destination++] = texture.data[source++];
                rgba[destination++] = texture.data[source++];
                rgba[destination++] = texture.data[source++];
                rgba[destination++] = texture.depth >= 4 ? texture.data[source++] : (byte)255;
            }
        }

        return new PreviewTextureData(texture.Name, texture.width, texture.height, rgba);
    }
}
