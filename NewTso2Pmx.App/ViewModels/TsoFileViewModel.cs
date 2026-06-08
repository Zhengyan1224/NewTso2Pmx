using NewTso2Pmx.Core.Loading;

namespace NewTso2Pmx.App.ViewModels;

public sealed class TsoFileViewModel : ViewModelBase
{
    private string _category;

    public TsoFileViewModel(LoadedTsoInfo source)
    {
        Source = source;
        _category = source.Category;
    }

    public LoadedTsoInfo Source { get; }

    public int Index => Source.Index;

    public string Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, value))
            {
                RaisePropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText => $"{Index + 1}. {Category} ({Source.Tso.meshes.Length} meshes)";
}
