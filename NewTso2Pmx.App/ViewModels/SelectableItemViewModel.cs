namespace NewTso2Pmx.App.ViewModels;

public sealed class SelectableItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public SelectableItemViewModel(string key, string displayName, bool isSelected = false)
    {
        Key = key;
        DisplayName = displayName;
        _isSelected = isSelected;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
