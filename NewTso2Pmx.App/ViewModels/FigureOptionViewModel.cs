namespace NewTso2Pmx.App.ViewModels;

public sealed class FigureOptionViewModel
{
    public FigureOptionViewModel(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public string DisplayText => $"Figure {Index + 1}";
}
