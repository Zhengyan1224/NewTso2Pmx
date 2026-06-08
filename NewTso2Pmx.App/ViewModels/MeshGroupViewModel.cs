using System.Collections.Generic;

namespace NewTso2Pmx.App.ViewModels;

public sealed class MeshGroupViewModel : ViewModelBase
{
    private bool _isSelected;

    public MeshGroupViewModel(string label, int vertexCount, IReadOnlyList<int> flatSubMeshIndices, bool isSelected)
    {
        Label = label;
        VertexCount = vertexCount;
        FlatSubMeshIndices = flatSubMeshIndices;
        _isSelected = isSelected;
    }

    public string Label { get; }

    public int VertexCount { get; }

    public IReadOnlyList<int> FlatSubMeshIndices { get; }

    public string DisplayText => $"{Label} : {VertexCount}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
