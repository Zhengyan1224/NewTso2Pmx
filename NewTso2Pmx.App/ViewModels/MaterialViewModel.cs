using NewTso2Pmx.Core.Loading;

namespace NewTso2Pmx.App.ViewModels;

public sealed class MaterialViewModel
{
    public MaterialViewModel(LoadedMaterialInfo source)
    {
        Source = source;
    }

    public LoadedMaterialInfo Source { get; }

    public string DisplayText => $"{Source.Category} / {Source.MaterialName}";
}
