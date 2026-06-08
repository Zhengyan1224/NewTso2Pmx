using TDCG;

namespace NewTso2Pmx.App.ViewModels;

public sealed class TsoSubScriptViewModel
{
    public TsoSubScriptViewModel(int tsoIndex, int index, TSOSubScript source)
    {
        TsoIndex = tsoIndex;
        Index = index;
        Source = source;
    }

    public int TsoIndex { get; }

    public int Index { get; }

    public TSOSubScript Source { get; }

    public string DisplayText => $"{Index + 1}. {Source.Name} ({Source.FileName})";
}
