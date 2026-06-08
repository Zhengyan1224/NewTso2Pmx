using TDCG;

namespace NewTso2Pmx.Core.Loading;

public sealed class LoadedDocument : IDisposable
{
    public LoadedDocument(
        string sourcePath,
        DocumentKind kind,
        Figure figure,
        IReadOnlyList<string> categories,
        IReadOnlyList<LoadedTsoInfo> tsoFiles,
        IReadOnlyList<FlatSubMeshInfo> subMeshes,
        IReadOnlyList<LoadedMaterialInfo> materials)
    {
        SourcePath = sourcePath;
        Kind = kind;
        Figure = figure;
        Categories = categories;
        TsoFiles = tsoFiles;
        SubMeshes = subMeshes;
        Materials = materials;
    }

    public string SourcePath { get; }

    public DocumentKind Kind { get; }

    public Figure Figure { get; }

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<LoadedTsoInfo> TsoFiles { get; }

    public IReadOnlyList<FlatSubMeshInfo> SubMeshes { get; }

    public IReadOnlyList<LoadedMaterialInfo> Materials { get; }

    public IReadOnlyList<MeshGroupDescriptor> CreateMeshGroups(MeshGroupingMode mode)
        => MeshGrouping.Build(SubMeshes, mode);

    public void Dispose()
    {
        Figure.Dispose();
    }
}
