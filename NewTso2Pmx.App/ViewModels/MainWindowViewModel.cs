using System.Collections.ObjectModel;
using NewTso2Pmx.Core.Loading;
using NewTso2Pmx.Core.Preview;

namespace NewTso2Pmx.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private string _sourcePath = string.Empty;
    private string _statusMessage = "请选择 `.tso`、`.png` 或包含 TSO 的目录。";
    private string _modelName = "自定义模型";
    private string _comment = "由 NewTso2Pmx 导出";
    private string _customOutputFolder = string.Empty;
    private bool _outputCreateSubfolder = true;
    private bool _outputUseSourceFolder;
    private bool _outputUseCustomFolder;
    private bool _useHumanBone = true;
    private bool _useSpheremap = true;
    private bool _useEdge;
    private bool _uniqueMaterial = true;
    private bool _enableHairPhysics = true;
    private bool _enableChestPhysics = true;
    private bool _enableSkirtPhysics = true;
    private string? _selectedHairTemplate;
    private string? _selectedChestTemplate;
    private string? _selectedSkirtTemplate;
    private string _documentSummary = "尚未加载模型。";
    private string _materialDetails = string.Empty;
    private string _previewSummary = "预览等待数据。";
    private MeshGroupingMode _selectedMeshGroupingMode;
    private ProportionViewModel? _selectedProportion;
    private double _selectedProportionValue;
    private MaterialViewModel? _selectedMaterial;
    private PreviewSceneData? _previewScene;

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ModelName
    {
        get => _modelName;
        set => SetProperty(ref _modelName, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public string CustomOutputFolder
    {
        get => _customOutputFolder;
        set => SetProperty(ref _customOutputFolder, value);
    }

    public bool OutputCreateSubfolder
    {
        get => _outputCreateSubfolder;
        set
        {
            if (SetProperty(ref _outputCreateSubfolder, value) && value)
            {
                OutputUseSourceFolder = false;
                OutputUseCustomFolder = false;
            }
        }
    }

    public bool OutputUseSourceFolder
    {
        get => _outputUseSourceFolder;
        set
        {
            if (SetProperty(ref _outputUseSourceFolder, value) && value)
            {
                OutputCreateSubfolder = false;
                OutputUseCustomFolder = false;
            }
        }
    }

    public bool OutputUseCustomFolder
    {
        get => _outputUseCustomFolder;
        set
        {
            if (SetProperty(ref _outputUseCustomFolder, value) && value)
            {
                OutputCreateSubfolder = false;
                OutputUseSourceFolder = false;
            }
        }
    }

    public bool UseHumanBone
    {
        get => _useHumanBone;
        set => SetProperty(ref _useHumanBone, value);
    }

    public bool UseSpheremap
    {
        get => _useSpheremap;
        set => SetProperty(ref _useSpheremap, value);
    }

    public bool UseEdge
    {
        get => _useEdge;
        set => SetProperty(ref _useEdge, value);
    }

    public bool UniqueMaterial
    {
        get => _uniqueMaterial;
        set => SetProperty(ref _uniqueMaterial, value);
    }

    public bool EnableHairPhysics
    {
        get => _enableHairPhysics;
        set => SetProperty(ref _enableHairPhysics, value);
    }

    public bool EnableChestPhysics
    {
        get => _enableChestPhysics;
        set => SetProperty(ref _enableChestPhysics, value);
    }

    public bool EnableSkirtPhysics
    {
        get => _enableSkirtPhysics;
        set => SetProperty(ref _enableSkirtPhysics, value);
    }

    public string? SelectedHairTemplate
    {
        get => _selectedHairTemplate;
        set => SetProperty(ref _selectedHairTemplate, value);
    }

    public string? SelectedChestTemplate
    {
        get => _selectedChestTemplate;
        set => SetProperty(ref _selectedChestTemplate, value);
    }

    public string? SelectedSkirtTemplate
    {
        get => _selectedSkirtTemplate;
        set => SetProperty(ref _selectedSkirtTemplate, value);
    }

    public string DocumentSummary
    {
        get => _documentSummary;
        set => SetProperty(ref _documentSummary, value);
    }

    public string MaterialDetails
    {
        get => _materialDetails;
        set => SetProperty(ref _materialDetails, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        set => SetProperty(ref _previewSummary, value);
    }

    public MeshGroupingMode SelectedMeshGroupingMode
    {
        get => _selectedMeshGroupingMode;
        set => SetProperty(ref _selectedMeshGroupingMode, value);
    }

    public ProportionViewModel? SelectedProportion
    {
        get => _selectedProportion;
        set
        {
            if (SetProperty(ref _selectedProportion, value))
            {
                SelectedProportionValue = value?.Ratio ?? 0.0;
                RaisePropertyChanged(nameof(HasSelectedProportion));
            }
        }
    }

    public double SelectedProportionValue
    {
        get => _selectedProportionValue;
        set => SetProperty(ref _selectedProportionValue, value);
    }

    public bool HasSelectedProportion => SelectedProportion is not null;

    public MaterialViewModel? SelectedMaterial
    {
        get => _selectedMaterial;
        set => SetProperty(ref _selectedMaterial, value);
    }

    public PreviewSceneData? PreviewScene
    {
        get => _previewScene;
        set => SetProperty(ref _previewScene, value);
    }

    public ObservableCollection<SelectableItemViewModel> BoneTables { get; } = [];

    public ObservableCollection<SelectableItemViewModel> ExtraPhysicsTemplates { get; } = [];

    public ObservableCollection<string> HairTemplates { get; } = [];

    public ObservableCollection<string> ChestTemplates { get; } = [];

    public ObservableCollection<string> SkirtTemplates { get; } = [];

    public ObservableCollection<ProportionViewModel> Proportions { get; } = [];

    public ObservableCollection<MeshGroupViewModel> MeshGroups { get; } = [];

    public ObservableCollection<MaterialViewModel> Materials { get; } = [];
}
