using TDCG;

namespace NewTso2Pmx.App.ViewModels;

public sealed class MorphOptionViewModel : ViewModelBase
{
    private double _ratio;

    public MorphOptionViewModel(string groupName, Morph source)
    {
        GroupName = groupName;
        Source = source;
    }

    public string GroupName { get; }

    public Morph Source { get; }

    public string Name => Source.Name;

    public string DisplayName => $"{GroupName} / {Name}";

    public double Ratio
    {
        get => _ratio;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (SetProperty(ref _ratio, clamped))
            {
                Source.Ratio = (float)clamped;
                RaisePropertyChanged(nameof(RatioText));
            }
        }
    }

    public string RatioText => Ratio.ToString("0.00");
}
