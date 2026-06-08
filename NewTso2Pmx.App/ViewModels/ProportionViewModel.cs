namespace NewTso2Pmx.App.ViewModels;

public sealed class ProportionViewModel : ViewModelBase
{
    private double _ratio;

    public ProportionViewModel(string key, string displayName, double ratio)
    {
        Key = key;
        DisplayName = displayName;
        _ratio = ratio;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public double Ratio
    {
        get => _ratio;
        set
        {
            if (SetProperty(ref _ratio, value))
            {
                RaisePropertyChanged(nameof(RatioText));
            }
        }
    }

    public string RatioText => $"{Ratio * 100.0:0}%";
}
