using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using NewTso2Pmx.App.ViewModels;
using NewTso2Pmx.Core.Infrastructure;
using NewTso2Pmx.Core.Loading;
using NewTso2Pmx.Core.Preview;
using TDCG;
using Tso2Pmd;

namespace NewTso2Pmx.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly TsoDocumentLoader _documentLoader = new();
    private readonly PreviewSceneBuilder _previewSceneBuilder = new();
    private readonly TemplateList _templateList = new();
    private readonly CorrespondTableList _correspondTableList = new();
    private readonly Dictionary<string, float> _proportionRatios = new(StringComparer.Ordinal);
    private readonly MeshGroupingOption[] _meshGroupingOptions =
    [
        new(MeshGroupingMode.ByMaterial, "按材质"),
        new(MeshGroupingMode.ByMesh, "按网格"),
        new(MeshGroupingMode.BySubMesh, "按子网格")
    ];

    private ComboBox _meshGroupingComboBox = null!;
    private LoadedDocument? _loadedDocument;
    private bool[] _selectedSubMeshes = [];
    private bool _isUpdatingSelectedProportion;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _meshGroupingComboBox = this.FindControl<ComboBox>("MeshGroupingComboBox")
            ?? throw new InvalidOperationException("找不到网格分组下拉框。");

        ConfigureMeshGroupingOptions();
        InitializeRuntimeResources();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _loadedDocument?.Dispose();
        _loadedDocument = null;
        base.OnClosed(e);
    }

    private void ConfigureMeshGroupingOptions()
    {
        _meshGroupingComboBox.ItemsSource = _meshGroupingOptions;
        _meshGroupingComboBox.SelectedItem = _meshGroupingOptions[0];
        _viewModel.SelectedMeshGroupingMode = _meshGroupingOptions[0].Mode;
    }

    private void InitializeRuntimeResources()
    {
        try
        {
            _templateList.Load();
            _correspondTableList.Load();

            InitializeBoneTables();
            InitializePhysicsTemplates();
            ResetProportionRatios(includePackagedConfig: true);
            PopulateProportionViewModels();

            _viewModel.StatusMessage = "就绪：请选择 .tso、.png 或包含 TSO 的目录。";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"初始化资源失败：{ex.Message}";
        }
    }

    private void InitializeBoneTables()
    {
        _viewModel.BoneTables.Clear();
        foreach (var name in _correspondTableList.NameList)
        {
            _viewModel.BoneTables.Add(new SelectableItemViewModel(name, name, false));
        }
    }

    private void InitializePhysicsTemplates()
    {
        _viewModel.HairTemplates.Clear();
        _viewModel.ChestTemplates.Clear();
        _viewModel.SkirtTemplates.Clear();
        _viewModel.ExtraPhysicsTemplates.Clear();

        foreach (var template in _templateList.phys_items)
        {
            switch (template.Group())
            {
                case 0:
                    _viewModel.HairTemplates.Add(template.Name());
                    break;
                case 1:
                    _viewModel.ChestTemplates.Add(template.Name());
                    break;
                case 2:
                    _viewModel.SkirtTemplates.Add(template.Name());
                    break;
                case 3:
                    _viewModel.ExtraPhysicsTemplates.Add(new SelectableItemViewModel(template.Name(), template.Name(), true));
                    break;
                case 4:
                    _viewModel.ExtraPhysicsTemplates.Add(new SelectableItemViewModel(template.Name(), template.Name(), false));
                    break;
            }
        }

        _viewModel.SelectedHairTemplate = _viewModel.HairTemplates.FirstOrDefault();
        _viewModel.SelectedChestTemplate = _viewModel.ChestTemplates.FirstOrDefault();
        _viewModel.SelectedSkirtTemplate = _viewModel.SkirtTemplates.FirstOrDefault();
    }

    private void ResetProportionRatios(bool includePackagedConfig)
    {
        _proportionRatios.Clear();

        foreach (var proportion in ProportionList.Instance.items)
        {
            var key = proportion.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            _proportionRatios[key] = 0.0f;
        }

        SetProportionRatioIfPresent("TDCG.Proportion.AAA_PMDInitPose", 1.0f);
        SetProportionRatioIfPresent("TDCG.Proportion.AAA_PMDInitPoseM", 1.0f);

        if (includePackagedConfig)
        {
            var configPath = LegacyPaths.Resolve("TPOConfig.xml");
            if (File.Exists(configPath))
            {
                ApplyTpoConfig(TPOConfig.Load(configPath));
            }
        }
    }

    private void ApplyTpoConfig(TPOConfig config)
    {
        foreach (var proportion in config.Proportions)
        {
            if (_proportionRatios.ContainsKey(proportion.ClassName))
            {
                _proportionRatios[proportion.ClassName] = proportion.Ratio;
            }
        }
    }

    private void PopulateProportionViewModels()
    {
        var selectedKey = _viewModel.SelectedProportion?.Key;
        _viewModel.Proportions.Clear();

        foreach (var pair in _proportionRatios
                     .Where(pair => !pair.Key.Contains("TDCG.Proportion.AAA", StringComparison.Ordinal))
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            _viewModel.Proportions.Add(new ProportionViewModel(
                pair.Key,
                GetProportionDisplayName(pair.Key),
                pair.Value));
        }

        _viewModel.SelectedProportion = _viewModel.Proportions.FirstOrDefault(item => item.Key == selectedKey)
            ?? _viewModel.Proportions.FirstOrDefault();
    }

    private static string GetProportionDisplayName(string key)
    {
        var name = key[(key.LastIndexOf('.') + 1)..];
        return name.Replace('_', ' ');
    }

    private async Task LoadSourceAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        _viewModel.StatusMessage = $"正在加载：{sourcePath}";

        try
        {
            var document = await Task.Run(() => _documentLoader.Load(sourcePath));

            _loadedDocument?.Dispose();
            _loadedDocument = document;
            _selectedSubMeshes = Enumerable.Repeat(true, document.SubMeshes.Count).ToArray();

            _viewModel.SourcePath = sourcePath;
            _viewModel.ModelName = SuggestModelName(sourcePath);
            _viewModel.DocumentSummary = BuildDocumentSummary(document);
            PopulateMaterials(document);

            ApplyCurrentProportionsToFigure();
            RebuildMeshGroups();
            RefreshPreviewScene();

            _viewModel.StatusMessage = $"已加载：{document.SubMeshes.Count} 个子网格，{document.Materials.Count} 个材质。";
        }
        catch (Exception ex)
        {
            _loadedDocument?.Dispose();
            _loadedDocument = null;
            _selectedSubMeshes = [];
            _viewModel.PreviewScene = PreviewSceneData.Empty;
            _viewModel.Materials.Clear();
            _viewModel.MeshGroups.Clear();
            _viewModel.MaterialDetails = string.Empty;
            _viewModel.DocumentSummary = "尚未加载模型。";
            _viewModel.PreviewSummary = "预览等待数据。";
            _viewModel.StatusMessage = $"加载失败：{ex.Message}";
        }
    }

    private void PopulateMaterials(LoadedDocument document)
    {
        _viewModel.Materials.Clear();
        foreach (var material in document.Materials)
        {
            _viewModel.Materials.Add(new MaterialViewModel(material));
        }

        _viewModel.SelectedMaterial = _viewModel.Materials.FirstOrDefault();
        UpdateMaterialDetails(_viewModel.SelectedMaterial);
    }

    private void UpdateMaterialDetails(MaterialViewModel? material)
    {
        if (material is null)
        {
            _viewModel.MaterialDetails = string.Empty;
            return;
        }

        var shaderLines = material.Source.Shader.GetLines();
        var builder = new StringBuilder();
        builder.AppendLine($"分类：{material.Source.Category}");
        builder.AppendLine($"材质：{material.Source.MaterialName}");
        builder.AppendLine($"脚本：{material.Source.ScriptFileName}");
        builder.AppendLine($"ColorTex：{material.Source.Shader.ColorTexName}");
        builder.AppendLine($"ShadeTex：{material.Source.Shader.ShadeTexName}");
        builder.AppendLine();
        builder.AppendLine("着色器参数：");

        foreach (var line in shaderLines)
        {
            builder.AppendLine(line);
        }

        _viewModel.MaterialDetails = builder.ToString().TrimEnd();
    }

    private void ApplyCurrentProportionsToFigure()
    {
        if (_loadedDocument is null)
        {
            return;
        }

        var figure = _loadedDocument.Figure;
        foreach (var pair in _proportionRatios)
        {
            var tpo = figure.TPOList[pair.Key];
            if (tpo is null)
            {
                continue;
            }

            if (pair.Key == "TDCG.Proportion.Shift_Y")
            {
                tpo.Ratio = 0.0f;
            }
            else
            {
                tpo.Ratio = pair.Value;
            }
        }

        figure.TransformTpo(figure.GetFrameIndex());
        figure.UpdateBoneMatrices(true);

        var toesNode = figure.TPOList.Tmo.FindNodeByName("W_LeftToes_End");
        var hipsShift = figure.TPOList["TDCG.Proportion.AAA_WHipsYShift"];
        if (toesNode is not null && hipsShift is not null)
        {
            hipsShift.Ratio += -toesNode.combined_matrix.M42 + 0.5f;
        }

        var shiftY = figure.TPOList["TDCG.Proportion.Shift_Y"];
        if (shiftY is not null && _proportionRatios.TryGetValue("TDCG.Proportion.Shift_Y", out var shiftRatio))
        {
            shiftY.Ratio = shiftRatio;
        }

        figure.TransformTpo(figure.GetFrameIndex());
        figure.UpdateBoneMatrices(true);
    }

    private void RebuildMeshGroups()
    {
        if (_loadedDocument is null)
        {
            _viewModel.MeshGroups.Clear();
            return;
        }

        var selectedMode = (_meshGroupingComboBox.SelectedItem as MeshGroupingOption)?.Mode ?? MeshGroupingMode.ByMaterial;
        _viewModel.SelectedMeshGroupingMode = selectedMode;

        var groups = _loadedDocument.CreateMeshGroups(selectedMode);
        _viewModel.MeshGroups.Clear();

        foreach (var group in groups)
        {
            var isSelected = group.FlatSubMeshIndices.All(index => _selectedSubMeshes[index]);
            var viewModel = new MeshGroupViewModel(group.Label, group.VertexCount, group.FlatSubMeshIndices, isSelected);
            viewModel.PropertyChanged += MeshGroupViewModelOnPropertyChanged;
            _viewModel.MeshGroups.Add(viewModel);
        }
    }

    private void MeshGroupViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MeshGroupViewModel.IsSelected) || sender is not MeshGroupViewModel group)
        {
            return;
        }

        foreach (var index in group.FlatSubMeshIndices)
        {
            _selectedSubMeshes[index] = group.IsSelected;
        }

        RefreshPreviewScene();
    }

    private void RefreshPreviewScene()
    {
        if (_loadedDocument is null)
        {
            _viewModel.PreviewScene = PreviewSceneData.Empty;
            _viewModel.PreviewSummary = "预览等待数据。";
            return;
        }

        _viewModel.PreviewScene = _previewSceneBuilder.Build(_loadedDocument, _selectedSubMeshes);
        UpdatePreviewSummary();
    }

    private void UpdatePreviewSummary()
    {
        if (_loadedDocument is null)
        {
            _viewModel.PreviewSummary = "预览等待数据。";
            return;
        }

        var selectedCount = _selectedSubMeshes.Count(selected => selected);
        var totalCount = _selectedSubMeshes.Length;
        var scene = _viewModel.PreviewScene;

        if (scene is null || scene.IsEmpty)
        {
            _viewModel.PreviewSummary = $"已选择 {selectedCount}/{totalCount} 个子网格，当前无可渲染三角形。";
            return;
        }

        _viewModel.PreviewSummary =
            $"已选择 {selectedCount}/{totalCount} 个子网格，顶点 {scene.Vertices.Count}，三角形 {scene.Indices.Count / 3}。";
    }

    private async void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 TSO 或 PNG 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("TSO / PNG")
                {
                    Patterns = ["*.tso", "*.TSO", "*.png"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadSourceAsync(path);
        }
    }

    private async void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanPickFolder)
        {
            _viewModel.StatusMessage = "当前平台不支持目录选择器。";
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择包含 TSO 的目录",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadSourceAsync(path);
        }
    }

    private async void BrowseOutputFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanPickFolder)
        {
            _viewModel.StatusMessage = "当前平台不支持目录选择器。";
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择输出目录",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _viewModel.CustomOutputFolder = path;
            _viewModel.OutputUseCustomFolder = true;
        }
    }

    private void RefreshPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        RefreshPreviewScene();
        _viewModel.StatusMessage = "预览已刷新。";
    }

    private async void ExportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        try
        {
            _viewModel.StatusMessage = "正在导出 PMX...";
            ApplyCurrentProportionsToFigure();

            var outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            await Task.Run(() =>
            {
                SyncCorrespondTableSelection();
                SyncPhysicsTemplateSelection();

                var converter = new TransTso2Pmd
                {
                    Figure = _loadedDocument.Figure,
                    Categories = _loadedDocument.Categories.ToList(),
                    UseMeshes = _selectedSubMeshes.ToList(),
                    UseOneBone = !_viewModel.UseHumanBone,
                    UseSpheremap = _viewModel.UseSpheremap,
                    UseEdge = _viewModel.UseEdge,
                    UniqueMaterial = _viewModel.UniqueMaterial,
                    TemplateList = _templateList,
                    CorTableList = _correspondTableList
                };

                converter.InputHeader(_viewModel.ModelName, _viewModel.Comment);
                converter.UpdatePmdFromFigure();
                converter.SavePmdFile(Path.Combine(outputDirectory, _viewModel.ModelName + ".pmx"));
                converter.OutputMaterialFile(outputDirectory, _viewModel.ModelName);
                SaveTpoConfig(outputDirectory);
            });

            _viewModel.StatusMessage = $"导出完成：{Path.Combine(outputDirectory, _viewModel.ModelName + ".pmx")}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出失败：{ex.Message}";
        }
    }

    private void SyncCorrespondTableSelection()
    {
        foreach (var item in _viewModel.BoneTables)
        {
            if (_correspondTableList.Selection.ContainsKey(item.Key))
            {
                _correspondTableList.Selection[item.Key] = item.IsSelected;
            }
        }
    }

    private void SyncPhysicsTemplateSelection()
    {
        var extraSelection = _viewModel.ExtraPhysicsTemplates.ToDictionary(item => item.Key, item => item.IsSelected, StringComparer.Ordinal);

        foreach (var template in _templateList.phys_items)
        {
            switch (template.Group())
            {
                case 0:
                    _templateList.phys_flags[template] = _viewModel.EnableHairPhysics
                        && string.Equals(template.Name(), _viewModel.SelectedHairTemplate, StringComparison.Ordinal);
                    break;
                case 1:
                    _templateList.phys_flags[template] = _viewModel.EnableChestPhysics
                        && string.Equals(template.Name(), _viewModel.SelectedChestTemplate, StringComparison.Ordinal);
                    break;
                case 2:
                    _templateList.phys_flags[template] = _viewModel.EnableSkirtPhysics
                        && string.Equals(template.Name(), _viewModel.SelectedSkirtTemplate, StringComparison.Ordinal);
                    break;
                case 3:
                case 4:
                    _templateList.phys_flags[template] = extraSelection.TryGetValue(template.Name(), out var isSelected) && isSelected;
                    break;
                default:
                    _templateList.phys_flags[template] = false;
                    break;
            }
        }
    }

    private string ResolveOutputDirectory()
    {
        var sourcePath = _loadedDocument?.SourcePath ?? throw new InvalidOperationException("尚未加载模型。");

        if (_viewModel.OutputUseCustomFolder)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.CustomOutputFolder))
            {
                throw new InvalidOperationException("请选择自定义输出目录。");
            }

            return _viewModel.CustomOutputFolder;
        }

        if (_viewModel.OutputUseSourceFolder)
        {
            return Directory.Exists(sourcePath)
                ? sourcePath
                : Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("无法确定源文件目录。");
        }

        if (Directory.Exists(sourcePath))
        {
            return Path.Combine(sourcePath, GetSourceBaseName(sourcePath));
        }

        return Path.Combine(
            Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("无法确定源文件目录。"),
            GetSourceBaseName(sourcePath));
    }

    private void SaveTpoConfig(string outputDirectory)
    {
        if (_loadedDocument is null)
        {
            return;
        }

        var config = new TPOConfig
        {
            Proportions = _loadedDocument.Figure.TPOList.files
                .Select(file => new Proportion
                {
                    ClassName = file.ProportionName,
                    Ratio = file.Ratio
                })
                .ToArray()
        };

        config.Save(Path.Combine(outputDirectory, "TPOConfig.xml"));
    }

    private async void ImportTpoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 TPOConfig.xml",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("XML")
                {
                    Patterns = ["*.xml"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            ResetProportionRatios(includePackagedConfig: false);
            ApplyTpoConfig(TPOConfig.Load(path));
            PopulateProportionViewModels();
            _viewModel.StatusMessage = $"已导入体型配置：{path}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导入体型配置失败：{ex.Message}";
        }
    }

    private void ApplyProportionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        ApplyCurrentProportionsToFigure();
        RefreshPreviewScene();
        _viewModel.StatusMessage = "体型参数已应用到预览。";
    }

    private void ProportionSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSelectedProportion || _viewModel.SelectedProportion is null)
        {
            return;
        }

        var ratio = (float)e.NewValue;
        _viewModel.SelectedProportion.Ratio = ratio;
        _proportionRatios[_viewModel.SelectedProportion.Key] = ratio;
    }

    private void IncreaseProportionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AdjustSelectedProportion(0.05);
    }

    private void DecreaseProportionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AdjustSelectedProportion(-0.05);
    }

    private void ResetCurrentProportionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetSelectedProportionValue(0.0);
    }

    private void ResetAllProportionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ResetProportionRatios(includePackagedConfig: false);

        foreach (var proportion in _viewModel.Proportions)
        {
            if (_proportionRatios.TryGetValue(proportion.Key, out var ratio))
            {
                proportion.Ratio = ratio;
            }
        }

        if (_viewModel.SelectedProportion is not null &&
            _proportionRatios.TryGetValue(_viewModel.SelectedProportion.Key, out var selectedRatio))
        {
            SetSelectedProportionValue(selectedRatio);
        }

        _viewModel.StatusMessage = "体型参数已全部重置。";
    }

    private void AdjustSelectedProportion(double delta)
    {
        if (_viewModel.SelectedProportion is null)
        {
            return;
        }

        var value = Math.Clamp(_viewModel.SelectedProportion.Ratio + delta, -2.0, 2.0);
        SetSelectedProportionValue(value);
    }

    private void SetSelectedProportionValue(double value)
    {
        if (_viewModel.SelectedProportion is null)
        {
            return;
        }

        _isUpdatingSelectedProportion = true;
        try
        {
            _viewModel.SelectedProportion.Ratio = value;
            _viewModel.SelectedProportionValue = value;
            _proportionRatios[_viewModel.SelectedProportion.Key] = (float)value;
        }
        finally
        {
            _isUpdatingSelectedProportion = false;
        }
    }

    private void MeshGroupingComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RebuildMeshGroups();
        RefreshPreviewScene();
    }

    private void MaterialListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateMaterialDetails(_viewModel.SelectedMaterial);
    }

    private static string SuggestModelName(string sourcePath)
    {
        var name = GetSourceBaseName(sourcePath);
        return name.Length <= 9 ? name : name[..9];
    }

    private static string GetSourceBaseName(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        var fileName = Path.GetFileName(sourcePath);
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (Path.GetExtension(withoutExtension).Equals(".tdcgsav", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(withoutExtension);
        }

        return withoutExtension;
    }

    private static string BuildDocumentSummary(LoadedDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"来源：{document.SourcePath}");
        builder.AppendLine($"类型：{GetDocumentKindDisplayName(document.Kind)}");
        builder.AppendLine($"分类数：{document.Categories.Count}");
        builder.AppendLine($"TSO 数：{document.TsoFiles.Count}");
        builder.AppendLine($"材质数：{document.Materials.Count}");
        builder.AppendLine($"子网格数：{document.SubMeshes.Count}");
        builder.AppendLine($"骨骼数：{document.Figure.Tmo.nodes?.Length ?? 0}");
        builder.AppendLine();
        builder.AppendLine("分类：");

        foreach (var category in document.Categories)
        {
            builder.AppendLine($"- {category}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetDocumentKindDisplayName(DocumentKind kind)
        => kind switch
        {
            DocumentKind.TsoFile => "单个 TSO 文件",
            DocumentKind.TsoDirectory => "TSO 目录",
            DocumentKind.SavePng => "角色保存 PNG",
            _ => kind.ToString()
        };

    private void SetProportionRatioIfPresent(string key, float value)
    {
        if (_proportionRatios.ContainsKey(key))
        {
            _proportionRatios[key] = value;
        }
    }

    private sealed record MeshGroupingOption(MeshGroupingMode Mode, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
