namespace NewTso2Pmx.Core.Loading;

public sealed class LoadedFigureSession : IDisposable
{
    private readonly List<LoadedFigureInfo> _figures;

    public LoadedFigureSession(
        string sourcePath,
        DocumentKind kind,
        IEnumerable<LoadedFigureInfo> figures)
    {
        SourcePath = sourcePath;
        Kind = kind;
        _figures = figures.ToList();
    }

    public string SourcePath { get; }

    public DocumentKind Kind { get; }

    public IReadOnlyList<LoadedFigureInfo> Figures => _figures;

    public void AddFigures(IEnumerable<LoadedFigureInfo> figures)
    {
        _figures.AddRange(figures);
    }

    public void InsertAt(int index, LoadedFigureInfo figure)
    {
        _figures.Insert(index, figure);
    }

    public LoadedFigureInfo RemoveAt(int index)
    {
        var figure = _figures[index];
        _figures.RemoveAt(index);
        return figure;
    }

    public IReadOnlyList<LoadedFigureInfo> DetachFigures()
    {
        var figures = _figures.ToArray();
        _figures.Clear();
        return figures;
    }

    public void Dispose()
    {
        foreach (var figure in _figures)
        {
            figure.Figure.Dispose();
        }

        _figures.Clear();
    }
}
