using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
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
    private TsoFileViewModel? _selectedTsoFile;
    private TsoSubScriptViewModel? _selectedTsoSubScript;
    private TsoTextureViewModel? _selectedTsoTexture;
    private FigureOptionViewModel? _selectedFigure;
    private string _textureDetails = string.Empty;
    private Bitmap? _texturePreview;
    private PreviewSceneData? _previewScene;
    private double _figureArmRatio = 0.5;
    private double _figureLegRatio = 0.5;
    private double _figureWaistRatio;
    private double _figureBustRatio = 0.5;
    private double _figureTallRatio = 0.5;
    private double _figureEyeRatio = 0.5;

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

    public TsoFileViewModel? SelectedTsoFile
    {
        get => _selectedTsoFile;
        set => SetProperty(ref _selectedTsoFile, value);
    }

    public TsoSubScriptViewModel? SelectedTsoSubScript
    {
        get => _selectedTsoSubScript;
        set => SetProperty(ref _selectedTsoSubScript, value);
    }

    public TsoTextureViewModel? SelectedTsoTexture
    {
        get => _selectedTsoTexture;
        set => SetProperty(ref _selectedTsoTexture, value);
    }

    public FigureOptionViewModel? SelectedFigure
    {
        get => _selectedFigure;
        set => SetProperty(ref _selectedFigure, value);
    }

    public string TextureDetails
    {
        get => _textureDetails;
        set => SetProperty(ref _textureDetails, value);
    }

    public Bitmap? TexturePreview
    {
        get => _texturePreview;
        set => SetProperty(ref _texturePreview, value);
    }

    public PreviewSceneData? PreviewScene
    {
        get => _previewScene;
        set => SetProperty(ref _previewScene, value);
    }

    public double FigureArmRatio
    {
        get => _figureArmRatio;
        set
        {
            if (SetProperty(ref _figureArmRatio, value))
            {
                RaisePropertyChanged(nameof(FigureArmRatioText));
            }
        }
    }

    public double FigureLegRatio
    {
        get => _figureLegRatio;
        set
        {
            if (SetProperty(ref _figureLegRatio, value))
            {
                RaisePropertyChanged(nameof(FigureLegRatioText));
            }
        }
    }

    public double FigureWaistRatio
    {
        get => _figureWaistRatio;
        set
        {
            if (SetProperty(ref _figureWaistRatio, value))
            {
                RaisePropertyChanged(nameof(FigureWaistRatioText));
            }
        }
    }

    public double FigureBustRatio
    {
        get => _figureBustRatio;
        set
        {
            if (SetProperty(ref _figureBustRatio, value))
            {
                RaisePropertyChanged(nameof(FigureBustRatioText));
            }
        }
    }

    public double FigureTallRatio
    {
        get => _figureTallRatio;
        set
        {
            if (SetProperty(ref _figureTallRatio, value))
            {
                RaisePropertyChanged(nameof(FigureTallRatioText));
            }
        }
    }

    public double FigureEyeRatio
    {
        get => _figureEyeRatio;
        set
        {
            if (SetProperty(ref _figureEyeRatio, value))
            {
                RaisePropertyChanged(nameof(FigureEyeRatioText));
            }
        }
    }

    public string FigureArmRatioText => FigureArmRatio.ToString("0.00");

    public string FigureLegRatioText => FigureLegRatio.ToString("0.00");

    public string FigureWaistRatioText => FigureWaistRatio.ToString("0.00");

    public string FigureBustRatioText => FigureBustRatio.ToString("0.00");

    public string FigureTallRatioText => FigureTallRatio.ToString("0.00");

    public string FigureEyeRatioText => FigureEyeRatio.ToString("0.00");

    public ObservableCollection<SelectableItemViewModel> BoneTables { get; } = [];

    public ObservableCollection<SelectableItemViewModel> ExtraPhysicsTemplates { get; } = [];

    public ObservableCollection<string> HairTemplates { get; } = [];

    public ObservableCollection<string> ChestTemplates { get; } = [];

    public ObservableCollection<string> SkirtTemplates { get; } = [];

    public ObservableCollection<ProportionViewModel> Proportions { get; } = [];

    public ObservableCollection<MeshGroupViewModel> MeshGroups { get; } = [];

    public ObservableCollection<MaterialViewModel> Materials { get; } = [];

    public ObservableCollection<ShaderParameterViewModel> ShaderParameters { get; } = [];

    public ObservableCollection<MorphOptionViewModel> Morphs { get; } = [];

    public ObservableCollection<string> TdcgCategories { get; } = [];

    public ObservableCollection<TsoFileViewModel> TsoFiles { get; } = [];

    public ObservableCollection<TsoSubScriptViewModel> TsoSubScripts { get; } = [];

    public ObservableCollection<TsoTextureViewModel> TsoTextures { get; } = [];

    public ObservableCollection<FigureOptionViewModel> Figures { get; } = [];
}
