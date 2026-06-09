using TDCG;

namespace NewTso2Pmx.App.ViewModels;

public sealed class TsoTextureViewModel : ViewModelBase
{
    public TsoTextureViewModel(int tsoIndex, int index, TSOTex source)
    {
        TsoIndex = tsoIndex;
        Index = index;
        Source = source;
    }

    public int TsoIndex { get; }

    public int Index { get; }

    public TSOTex Source { get; }

    public string DisplayText => $"{Index + 1}. {Source.Name} ({Source.width}x{Source.height}, {Source.depth * 8}bit)";

    public string Details =>
        $"名称：{Source.Name}\n文件：{Source.FileName}\n尺寸：{Source.width} x {Source.height}\n色深：{Source.depth * 8} bit\n数据：{Source.data.Length:N0} bytes";

    public void Refresh()
    {
        RaisePropertyChanged(nameof(DisplayText));
        RaisePropertyChanged(nameof(Details));
    }
}
