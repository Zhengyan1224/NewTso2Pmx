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
        IReadOnlyList<LoadedMaterialInfo> materials,
        int figureIndex = 0,
        int figureCount = 1,
        bool ownsFigure = true)
    {
        SourcePath = sourcePath;
        Kind = kind;
        Figure = figure;
        Categories = categories;
        TsoFiles = tsoFiles;
        SubMeshes = subMeshes;
        Materials = materials;
        FigureIndex = figureIndex;
        FigureCount = figureCount;
        OwnsFigure = ownsFigure;
    }

    public string SourcePath { get; }

    public DocumentKind Kind { get; }

    public Figure Figure { get; }

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<LoadedTsoInfo> TsoFiles { get; }

    public IReadOnlyList<FlatSubMeshInfo> SubMeshes { get; }

    public IReadOnlyList<LoadedMaterialInfo> Materials { get; }

    public int FigureIndex { get; }

    public int FigureCount { get; }

    public bool OwnsFigure { get; }

    public IReadOnlyList<MeshGroupDescriptor> CreateMeshGroups(MeshGroupingMode mode)
        => MeshGrouping.Build(SubMeshes, mode);

    public void Dispose()
    {
        if (OwnsFigure)
        {
            Figure.Dispose();
        }
    }
}
