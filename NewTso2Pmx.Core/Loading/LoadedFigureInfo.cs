using TDCG;

namespace NewTso2Pmx.Core.Loading;

public sealed class LoadedFigureInfo
{
    public LoadedFigureInfo(Figure figure, IReadOnlyList<string> categories)
    {
        Figure = figure;
        Categories = categories.ToList();
    }

    public Figure Figure { get; }

    public List<string> Categories { get; }
}
