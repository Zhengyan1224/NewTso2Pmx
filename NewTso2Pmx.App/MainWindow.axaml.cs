using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using NewTso2Pmx.App.Configuration;
using NewTso2Pmx.App.ViewModels;
using NewTso2Pmx.Core.Infrastructure;
using NewTso2Pmx.Core.Loading;
using NewTso2Pmx.Core.Preview;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
    private readonly Morphing _morphing = new();
    private readonly AppSettings _settings;
    private readonly Dictionary<string, float> _proportionRatios = new(StringComparer.Ordinal);
    private readonly MeshGroupingOption[] _meshGroupingOptions =
    [
        new(MeshGroupingMode.ByMaterial, "按材质"),
        new(MeshGroupingMode.ByMesh, "按网格"),
        new(MeshGroupingMode.BySubMesh, "按子网格")
    ];

    private ComboBox _meshGroupingComboBox = null!;
    private LoadedFigureSession? _loadedSession;
    private LoadedDocument? _loadedDocument;
    private bool[] _selectedSubMeshes = [];
    private bool _isUpdatingSelectedProportion;
    private bool _isUpdatingFigureSliders;
    private bool _isUpdatingMorphs;
    private bool _isUpdatingTsoFiles;
    private bool _isUpdatingFigures;
    private bool _isApplyingSettings;

    private const string RecentInputDirectoryKey = "Input";
    private const string RecentOutputDirectoryKey = "Output";

    public MainWindow()
    {
        _settings = AppSettingsStore.Load();
        InitializeComponent();
        DataContext = _viewModel;

        _meshGroupingComboBox = this.FindControl<ComboBox>("MeshGroupingComboBox")
            ?? throw new InvalidOperationException("找不到网格分组下拉框。");

        ConfigureMeshGroupingOptions();
        InitializeRuntimeResources();
        SubscribeSettingsChanges();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettingsFromViewModel();
        _loadedDocument?.Dispose();
        _loadedDocument = null;
        _loadedSession?.Dispose();
        _loadedSession = null;
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
            _morphing.Load(LegacyPaths.Resolve("表情"));

            InitializeTdcgCategories();
            InitializeBoneTables();
            InitializePhysicsTemplates();
            InitializeMorphs();
            ResetProportionRatios(includePackagedConfig: true);
            PopulateProportionViewModels();
            ApplySettingsToViewModel();

            _viewModel.StatusMessage = "就绪：请选择 .tso、.png 或包含 TSO 的目录。";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"初始化资源失败：{ex.Message}";
        }
    }

    private void InitializeTdcgCategories()
    {
        _viewModel.TdcgCategories.Clear();
        foreach (var category in TdcgPngExporter.Categories)
        {
            _viewModel.TdcgCategories.Add(category);
        }
    }

    private void InitializeMorphs()
    {
        _viewModel.Morphs.Clear();
        foreach (var group in _morphing.Groups)
        {
            foreach (var morph in group.Items)
            {
                var item = new MorphOptionViewModel(group.Name, morph);
                item.PropertyChanged += MorphOptionViewModelOnPropertyChanged;
                _viewModel.Morphs.Add(item);
            }
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

    private void ApplySettingsToViewModel()
    {
        _isApplyingSettings = true;
        try
        {
            _viewModel.Comment = _settings.Comment;
            _viewModel.CustomOutputFolder = _settings.CustomOutputFolder;
            _viewModel.OutputCreateSubfolder = _settings.OutputMode == AppOutputModes.CreateSubfolder;
            _viewModel.OutputUseSourceFolder = _settings.OutputMode == AppOutputModes.SourceFolder;
            _viewModel.OutputUseCustomFolder = _settings.OutputMode == AppOutputModes.CustomFolder;
            _viewModel.UseHumanBone = _settings.UseHumanBone;
            _viewModel.UseSpheremap = _settings.UseSpheremap;
            _viewModel.UseEdge = _settings.UseEdge;
            _viewModel.UniqueMaterial = _settings.UniqueMaterial;
            _viewModel.EnableHairPhysics = _settings.EnableHairPhysics;
            _viewModel.EnableChestPhysics = _settings.EnableChestPhysics;
            _viewModel.EnableSkirtPhysics = _settings.EnableSkirtPhysics;

            ApplyCollectionSelection(_viewModel.BoneTables, _settings.BoneTableSelection);
            ApplyCollectionSelection(_viewModel.ExtraPhysicsTemplates, _settings.ExtraPhysicsSelection);
            SelectTemplateIfPresent(_viewModel.HairTemplates, _settings.SelectedHairTemplate, value => _viewModel.SelectedHairTemplate = value);
            SelectTemplateIfPresent(_viewModel.ChestTemplates, _settings.SelectedChestTemplate, value => _viewModel.SelectedChestTemplate = value);
            SelectTemplateIfPresent(_viewModel.SkirtTemplates, _settings.SelectedSkirtTemplate, value => _viewModel.SelectedSkirtTemplate = value);

            var grouping = _meshGroupingOptions.FirstOrDefault(option => option.Mode == _settings.MeshGroupingMode)
                           ?? _meshGroupingOptions[0];
            _meshGroupingComboBox.SelectedItem = grouping;
            _viewModel.SelectedMeshGroupingMode = grouping.Mode;
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private static void ApplyCollectionSelection(
        IEnumerable<SelectableItemViewModel> items,
        IReadOnlyDictionary<string, bool> selections)
    {
        foreach (var item in items)
        {
            if (selections.TryGetValue(item.Key, out var isSelected))
            {
                item.IsSelected = isSelected;
            }
        }
    }

    private static void SelectTemplateIfPresent(
        IEnumerable<string> templates,
        string? selectedTemplate,
        Action<string?> apply)
    {
        if (!string.IsNullOrWhiteSpace(selectedTemplate) &&
            templates.Contains(selectedTemplate, StringComparer.Ordinal))
        {
            apply(selectedTemplate);
        }
    }

    private void SubscribeSettingsChanges()
    {
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.StatusMessage) or
                nameof(MainWindowViewModel.SourcePath) or
                nameof(MainWindowViewModel.ModelName) or
                nameof(MainWindowViewModel.DocumentSummary) or
                nameof(MainWindowViewModel.MaterialDetails) or
                nameof(MainWindowViewModel.PreviewSummary) or
                nameof(MainWindowViewModel.TextureDetails) or
                nameof(MainWindowViewModel.TexturePreview) or
                nameof(MainWindowViewModel.PreviewScene) or
                nameof(MainWindowViewModel.SelectedMaterial) or
                nameof(MainWindowViewModel.SelectedTsoFile) or
                nameof(MainWindowViewModel.SelectedTsoSubScript) or
                nameof(MainWindowViewModel.SelectedTsoTexture) or
                nameof(MainWindowViewModel.SelectedFigure) or
                nameof(MainWindowViewModel.SelectedProportion) or
                nameof(MainWindowViewModel.SelectedProportionValue))
            {
                return;
            }

            SaveSettingsFromViewModel();
        };

        foreach (var item in _viewModel.BoneTables)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectableItemViewModel.IsSelected))
                {
                    SaveSettingsFromViewModel();
                }
            };
        }

        foreach (var item in _viewModel.ExtraPhysicsTemplates)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectableItemViewModel.IsSelected))
                {
                    SaveSettingsFromViewModel();
                }
            };
        }
    }

    private void SaveSettingsFromViewModel()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _settings.Comment = _viewModel.Comment;
        _settings.CustomOutputFolder = _viewModel.CustomOutputFolder;
        _settings.OutputMode = _viewModel.OutputUseCustomFolder
            ? AppOutputModes.CustomFolder
            : _viewModel.OutputUseSourceFolder
                ? AppOutputModes.SourceFolder
                : AppOutputModes.CreateSubfolder;
        _settings.UseHumanBone = _viewModel.UseHumanBone;
        _settings.UseSpheremap = _viewModel.UseSpheremap;
        _settings.UseEdge = _viewModel.UseEdge;
        _settings.UniqueMaterial = _viewModel.UniqueMaterial;
        _settings.EnableHairPhysics = _viewModel.EnableHairPhysics;
        _settings.EnableChestPhysics = _viewModel.EnableChestPhysics;
        _settings.EnableSkirtPhysics = _viewModel.EnableSkirtPhysics;
        _settings.SelectedHairTemplate = _viewModel.SelectedHairTemplate;
        _settings.SelectedChestTemplate = _viewModel.SelectedChestTemplate;
        _settings.SelectedSkirtTemplate = _viewModel.SelectedSkirtTemplate;
        _settings.MeshGroupingMode = _viewModel.SelectedMeshGroupingMode;
        _settings.BoneTableSelection = _viewModel.BoneTables.ToDictionary(item => item.Key, item => item.IsSelected, StringComparer.Ordinal);
        _settings.ExtraPhysicsSelection = _viewModel.ExtraPhysicsTemplates.ToDictionary(item => item.Key, item => item.IsSelected, StringComparer.Ordinal);

        try
        {
            AppSettingsStore.Save(_settings);
        }
        catch
        {
            // Settings persistence should not block conversion or editing workflows.
        }
    }

    private async Task<IStorageFolder?> GetSuggestedStartLocationAsync(string recentKey)
    {
        if (_settings.RecentDirectories.TryGetValue(recentKey, out var recentDirectory) &&
            Directory.Exists(recentDirectory))
        {
            try
            {
                return await StorageProvider.TryGetFolderFromPathAsync(recentDirectory);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<IStorageFile>> OpenFilePickerWithRecentAsync(
        FilePickerOpenOptions options,
        string recentKey)
    {
        options.SuggestedStartLocation ??= await GetSuggestedStartLocationAsync(recentKey);
        var files = await StorageProvider.OpenFilePickerAsync(options);
        RememberRecentDirectory(recentKey, files.FirstOrDefault()?.TryGetLocalPath());
        return files;
    }

    private async Task<IStorageFile?> SaveFilePickerWithRecentAsync(
        FilePickerSaveOptions options,
        string recentKey)
    {
        options.SuggestedStartLocation ??= await GetSuggestedStartLocationAsync(recentKey);
        var file = await StorageProvider.SaveFilePickerAsync(options);
        RememberRecentDirectory(recentKey, file?.TryGetLocalPath());
        return file;
    }

    private async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerWithRecentAsync(
        FolderPickerOpenOptions options,
        string recentKey)
    {
        options.SuggestedStartLocation ??= await GetSuggestedStartLocationAsync(recentKey);
        var folders = await StorageProvider.OpenFolderPickerAsync(options);
        RememberRecentDirectory(recentKey, folders.FirstOrDefault()?.TryGetLocalPath());
        return folders;
    }

    private void RememberRecentDirectory(string recentKey, string? path)
    {
        var directory = TryGetDirectory(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        _settings.RecentDirectories[recentKey] = directory;
        SaveSettingsFromViewModel();
    }

    private static string? TryGetDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path);
        }
        catch
        {
            return null;
        }
    }

    private LoadedDocument RestoreEditSessionIfMatching(LoadedDocument document, string displaySourcePath)
    {
        var session = _settings.EditSession;
        if (!IsSameSessionSource(session.SourcePath, displaySourcePath))
        {
            return document;
        }

        if (session.SelectedSubMeshes.Count == document.SubMeshes.Count)
        {
            _selectedSubMeshes = session.SelectedSubMeshes.ToArray();
        }

        if (session.TsoCategories.Count == document.TsoFiles.Count)
        {
            var categories = document.Categories.ToList();
            for (var index = 0; index < document.TsoFiles.Count; index++)
            {
                var category = session.TsoCategories[index];
                if (string.IsNullOrWhiteSpace(category))
                {
                    continue;
                }

                if (index < categories.Count)
                {
                    categories[index] = category;
                }
                else
                {
                    categories.Add(category);
                }
            }

            _loadedDocument = _documentLoader.Rebuild(document, categories);
            document = _loadedDocument;
            SyncCurrentSessionCategories(categories);
        }

        return document;
    }

    private void RestoreEditSessionSelectionIfMatching(string displaySourcePath)
    {
        var session = _settings.EditSession;
        if (!IsSameSessionSource(session.SourcePath, displaySourcePath))
        {
            return;
        }

        _viewModel.SelectedTsoFile = _viewModel.TsoFiles.FirstOrDefault(item => item.Index == session.SelectedTsoIndex)
            ?? _viewModel.TsoFiles.FirstOrDefault();
        PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
        PopulateTsoTextures(_viewModel.SelectedTsoFile);
    }

    private void SaveEditSessionSnapshot()
    {
        if (_loadedDocument is null)
        {
            return;
        }

        _settings.EditSession.SourcePath = _loadedDocument.SourcePath;
        _settings.EditSession.FigureIndex = _loadedDocument.FigureIndex;
        _settings.EditSession.SelectedTsoIndex = _viewModel.SelectedTsoFile?.Index ?? 0;
        _settings.EditSession.TsoCategories = _viewModel.TsoFiles.Count > 0
            ? _viewModel.TsoFiles.OrderBy(item => item.Index).Select(item => item.Category).ToList()
            : _loadedDocument.Categories.ToList();
        _settings.EditSession.SelectedSubMeshes = _selectedSubMeshes.ToList();
        SaveSettingsFromViewModel();
    }

    private static bool IsSameSessionSource(string? savedSourcePath, string currentSourcePath)
    {
        if (string.IsNullOrWhiteSpace(savedSourcePath) || string.IsNullOrWhiteSpace(currentSourcePath))
        {
            return false;
        }

        return string.Equals(savedSourcePath, currentSourcePath, StringComparison.OrdinalIgnoreCase);
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

    private async Task LoadSourceAsync(string sourcePath, int figureIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        _viewModel.StatusMessage = $"正在加载：{sourcePath}";

        try
        {
            var session = await Task.Run(() => _documentLoader.LoadSession(sourcePath));
            var resolvedFigureIndex = IsSameSessionSource(_settings.EditSession.SourcePath, sourcePath)
                ? Math.Clamp(_settings.EditSession.FigureIndex, 0, session.Figures.Count - 1)
                : figureIndex;
            var document = _documentLoader.CreateDocument(session, resolvedFigureIndex);

            _loadedDocument?.Dispose();
            _loadedSession?.Dispose();
            _loadedSession = session;
            _loadedDocument = document;
            SetActiveDocument(document, sourcePath);
            SaveEditSessionSnapshot();

            _viewModel.StatusMessage = $"已加载：{document.SubMeshes.Count} 个子网格，{document.Materials.Count} 个材质。";
        }
        catch (Exception ex)
        {
            ClearLoadedState();
            _viewModel.StatusMessage = $"加载失败：{ex.Message}";
        }
    }

    private async Task LoadSourcesAsync(IReadOnlyList<string> sourcePaths)
    {
        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        if (paths.Length == 1)
        {
            await LoadSourceAsync(paths[0]);
            return;
        }

        _viewModel.StatusMessage = $"正在加载 {paths.Length} 个 TSO 文件...";

        try
        {
            var session = await Task.Run(() => _documentLoader.LoadSession(paths));
            var document = _documentLoader.CreateDocument(session, 0);

            _loadedDocument?.Dispose();
            _loadedSession?.Dispose();
            _loadedSession = session;
            _loadedDocument = document;
            SetActiveDocument(document, string.Join("; ", paths), paths[0]);
            SaveEditSessionSnapshot();

            _viewModel.StatusMessage =
                $"已加载：{document.TsoFiles.Count} 个 TSO，{document.SubMeshes.Count} 个子网格，{document.Materials.Count} 个材质。";
        }
        catch (Exception ex)
        {
            ClearLoadedState();
            _viewModel.StatusMessage = $"加载失败：{ex.Message}";
        }
    }

    private void SetActiveDocument(
        LoadedDocument document,
        string displaySourcePath,
        string? modelSourcePath = null,
        bool resetModelName = true)
    {
        _selectedSubMeshes = Enumerable.Repeat(true, document.SubMeshes.Count).ToArray();

        _viewModel.SourcePath = displaySourcePath;
        if (resetModelName)
        {
            _viewModel.ModelName = SuggestModelName(modelSourcePath ?? displaySourcePath);
        }

        document = RestoreEditSessionIfMatching(document, document.SourcePath);
        _viewModel.DocumentSummary = BuildDocumentSummary(document);
        PopulateMaterials(document);
        PopulateTsoFiles(document);
        RestoreEditSessionSelectionIfMatching(document.SourcePath);
        PopulateFigureOptions(document);
        PopulateFigureSliders(document);
        ResetMorphRatios();

        ApplyCurrentProportionsToFigure();
        RebuildMeshGroups();
        RefreshPreviewScene();
    }

    private void ClearLoadedState()
    {
        _loadedDocument?.Dispose();
        _loadedDocument = null;
        _loadedSession?.Dispose();
        _loadedSession = null;
        _selectedSubMeshes = [];
        _viewModel.PreviewScene = PreviewSceneData.Empty;
        _viewModel.Materials.Clear();
        _viewModel.ShaderParameters.Clear();
        _viewModel.TsoFiles.Clear();
        _viewModel.TsoSubScripts.Clear();
        _viewModel.TsoTextures.Clear();
        _viewModel.Figures.Clear();
        _viewModel.MeshGroups.Clear();
        _viewModel.MaterialDetails = string.Empty;
        _viewModel.TextureDetails = string.Empty;
        SetTexturePreview(null);
        _viewModel.DocumentSummary = "尚未加载模型。";
        _viewModel.PreviewSummary = "预览等待数据。";
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

    private void PopulateTsoFiles(LoadedDocument document)
    {
        var selectedIndex = _viewModel.SelectedTsoFile?.Index;
        _isUpdatingTsoFiles = true;
        try
        {
            _viewModel.TsoFiles.Clear();
            foreach (var tsoFile in document.TsoFiles)
            {
                var item = new TsoFileViewModel(tsoFile);
                item.PropertyChanged += TsoFileViewModelOnPropertyChanged;
                _viewModel.TsoFiles.Add(item);
            }

            _viewModel.SelectedTsoFile = _viewModel.TsoFiles.FirstOrDefault(item => item.Index == selectedIndex)
                ?? _viewModel.TsoFiles.FirstOrDefault();
        }
        finally
        {
            _isUpdatingTsoFiles = false;
        }

        PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
        PopulateTsoTextures(_viewModel.SelectedTsoFile);
    }

    private void TsoFileViewModelOnPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TsoFileViewModel.Category) || sender is not TsoFileViewModel tsoFile)
        {
            return;
        }

        if (_isUpdatingTsoFiles)
        {
            return;
        }

        ApplyTsoCategory(tsoFile);
    }

    private void PopulateTsoSubScripts(TsoFileViewModel? tsoFile)
    {
        _viewModel.TsoSubScripts.Clear();
        if (tsoFile is null)
        {
            _viewModel.SelectedTsoSubScript = null;
            return;
        }

        for (var index = 0; index < tsoFile.Source.Tso.sub_scripts.Length; index++)
        {
            _viewModel.TsoSubScripts.Add(new TsoSubScriptViewModel(
                tsoFile.Index,
                index,
                tsoFile.Source.Tso.sub_scripts[index]));
        }

        _viewModel.SelectedTsoSubScript = _viewModel.TsoSubScripts.FirstOrDefault();
    }

    private void PopulateTsoTextures(TsoFileViewModel? tsoFile)
    {
        _viewModel.TsoTextures.Clear();
        if (tsoFile is null)
        {
            _viewModel.SelectedTsoTexture = null;
            _viewModel.TextureDetails = string.Empty;
            SetTexturePreview(null);
            return;
        }

        for (var index = 0; index < tsoFile.Source.Tso.textures.Length; index++)
        {
            _viewModel.TsoTextures.Add(new TsoTextureViewModel(
                tsoFile.Index,
                index,
                tsoFile.Source.Tso.textures[index]));
        }

        _viewModel.SelectedTsoTexture = _viewModel.TsoTextures.FirstOrDefault();
        UpdateTexturePreview(_viewModel.SelectedTsoTexture);
    }

    private void PopulateFigureOptions(LoadedDocument document)
    {
        _isUpdatingFigures = true;
        try
        {
            _viewModel.Figures.Clear();
            for (var index = 0; index < document.FigureCount; index++)
            {
                _viewModel.Figures.Add(new FigureOptionViewModel(index));
            }

            _viewModel.SelectedFigure = _viewModel.Figures.FirstOrDefault(item => item.Index == document.FigureIndex);
        }
        finally
        {
            _isUpdatingFigures = false;
        }
    }

    private void RebuildLoadedDocument(IReadOnlyList<string> categories)
    {
        if (_loadedDocument is null)
        {
            return;
        }

        _loadedDocument = _documentLoader.Rebuild(_loadedDocument, categories);
        SyncCurrentSessionCategories(categories);
        _selectedSubMeshes = Enumerable.Repeat(true, _loadedDocument.SubMeshes.Count).ToArray();

        _viewModel.DocumentSummary = BuildDocumentSummary(_loadedDocument);
        PopulateMaterials(_loadedDocument);
        PopulateTsoFiles(_loadedDocument);
        PopulateFigureOptions(_loadedDocument);
        PopulateFigureSliders(_loadedDocument);
        ResetMorphRatios();
        ApplyCurrentProportionsToFigure();
        RebuildMeshGroups();
        RefreshPreviewScene();
        SaveEditSessionSnapshot();
    }

    private void SyncCurrentSessionCategories(IReadOnlyList<string> categories)
    {
        if (_loadedSession is null || _loadedDocument is null)
        {
            return;
        }

        if (_loadedDocument.FigureIndex < 0 || _loadedDocument.FigureIndex >= _loadedSession.Figures.Count)
        {
            return;
        }

        var target = _loadedSession.Figures[_loadedDocument.FigureIndex].Categories;
        target.Clear();
        target.AddRange(categories);
    }

    private void RefreshCurrentDocument(IReadOnlyList<string> categories, int? selectedTsoIndex = null)
    {
        if (_loadedDocument is null)
        {
            return;
        }

        _loadedDocument.Figure.UpdateNodeMapAndBoneMatrices();
        RebuildLoadedDocument(categories);

        if (selectedTsoIndex is not null)
        {
            _viewModel.SelectedTsoFile = _viewModel.TsoFiles.FirstOrDefault(item => item.Index == selectedTsoIndex.Value)
                ?? _viewModel.TsoFiles.LastOrDefault();
            PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
            PopulateTsoTextures(_viewModel.SelectedTsoFile);
        }

        SaveEditSessionSnapshot();
    }

    private void ApplyTsoCategory(TsoFileViewModel changedTsoFile)
    {
        if (_loadedDocument is null)
        {
            return;
        }

        var categories = _viewModel.TsoFiles
            .OrderBy(item => item.Index)
            .Select(item => string.IsNullOrWhiteSpace(item.Category)
                ? (_loadedDocument.Categories.Count > item.Index ? _loadedDocument.Categories[item.Index] : $"对象 {item.Index + 1}")
                : item.Category)
            .ToList();

        RebuildLoadedDocument(categories);
        _viewModel.SelectedTsoFile = _viewModel.TsoFiles.FirstOrDefault(item => item.Index == changedTsoFile.Index);
        PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
        PopulateTsoTextures(_viewModel.SelectedTsoFile);
        SaveEditSessionSnapshot();
        _viewModel.StatusMessage = $"TSO 分类已更新：{changedTsoFile.Category}";
    }

    private void UpdateMaterialDetails(MaterialViewModel? material)
    {
        if (material is null)
        {
            _viewModel.MaterialDetails = string.Empty;
            _viewModel.ShaderParameters.Clear();
            return;
        }

        _viewModel.ShaderParameters.Clear();
        foreach (var parameter in material.Source.Shader.shader_parameters)
        {
            var parameterViewModel = new ShaderParameterViewModel(parameter);
            parameterViewModel.PropertyChanged += ShaderParameterViewModelOnPropertyChanged;
            _viewModel.ShaderParameters.Add(parameterViewModel);
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

    private void UpdateMaterialSummaryText(MaterialViewModel material)
    {
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

    private void ShaderParameterViewModelOnPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel.SelectedMaterial is null)
        {
            return;
        }

        UpdateMaterialSummaryText(_viewModel.SelectedMaterial);
        _viewModel.StatusMessage = "Shader 参数已更新，导出会使用当前材质参数。";
    }

    private async void CopyShaderDumpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedMaterial is null)
        {
            _viewModel.StatusMessage = "请先选择材质。";
            return;
        }

        var dump = BuildShaderDump(_viewModel.SelectedMaterial);
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _viewModel.StatusMessage = "当前平台不支持剪贴板。";
            return;
        }

        try
        {
            await clipboard.SetTextAsync(dump);
            _viewModel.StatusMessage = "Shader 参数 Dump 已复制到剪贴板。";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"复制 Shader 参数 Dump 失败：{ex.Message}";
        }
    }

    private async void ExportAllShaderDumpsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.Materials.Count == 0)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var textType = CreateTextFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存全部 Shader Dump",
            SuggestedFileName = $"{GetSourceBaseName(_loadedDocument.SourcePath)}_shader_dump.txt",
            DefaultExtension = "txt",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                textType
            ],
            SuggestedFileType = textType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            var dump = BuildAllShaderDumps(_viewModel.Materials);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(dump);
            _viewModel.StatusMessage = $"已导出 Shader Dump：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出 Shader Dump 失败：{ex.Message}";
        }
    }

    private static string BuildShaderDump(MaterialViewModel material)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-- dump shader parameters --");
        builder.AppendLine($"TSO {material.Source.TsoIndex}");
        builder.AppendLine($"MaterialIndex {material.Source.MaterialIndex}");
        builder.AppendLine($"Category {material.Source.Category}");
        builder.AppendLine($"Material {material.Source.MaterialName}");
        builder.AppendLine($"Script {material.Source.ScriptFileName}");
        builder.AppendLine();
        builder.AppendLine("-- shader lines --");

        foreach (var line in material.Source.Shader.GetLines())
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.AppendLine("-- shader parameters --");

        foreach (var parameter in material.Source.Shader.shader_parameters)
        {
            builder.AppendLine(
                $"Name {parameter.Name} F1 {parameter.F1} F2 {parameter.F2} F3 {parameter.F3} F4 {parameter.F4}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildAllShaderDumps(IEnumerable<MaterialViewModel> materials)
    {
        var builder = new StringBuilder();
        foreach (var material in materials.OrderBy(item => item.Source.TsoIndex).ThenBy(item => item.Source.MaterialIndex))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.AppendLine(BuildShaderDump(material));
        }

        return builder.ToString().TrimEnd();
    }

    private void SaveCurrentShaderEdits()
    {
        if (_loadedDocument is null)
        {
            return;
        }

        foreach (var tso in _loadedDocument.Figure.TSOList)
        {
            foreach (var subScript in tso.sub_scripts)
            {
                subScript.SaveShader();
            }
        }
    }

    private void PopulateFigureSliders(LoadedDocument document)
    {
        var sliderMatrix = document.Figure.slider_matrix;
        _isUpdatingFigureSliders = true;
        try
        {
            _viewModel.FigureArmRatio = sliderMatrix.ArmRatio;
            _viewModel.FigureLegRatio = sliderMatrix.LegRatio;
            _viewModel.FigureWaistRatio = sliderMatrix.WaistRatio;
            _viewModel.FigureBustRatio = sliderMatrix.BustRatio;
            _viewModel.FigureTallRatio = sliderMatrix.TallRatio;
            _viewModel.FigureEyeRatio = sliderMatrix.EyeRatio;
        }
        finally
        {
            _isUpdatingFigureSliders = false;
        }
    }

    private void ApplyFigureSlidersToFigure(Figure figure)
    {
        var sliderMatrix = figure.slider_matrix;
        sliderMatrix.ArmRatio = (float)_viewModel.FigureArmRatio;
        sliderMatrix.LegRatio = (float)_viewModel.FigureLegRatio;
        sliderMatrix.WaistRatio = (float)_viewModel.FigureWaistRatio;
        sliderMatrix.BustRatio = (float)_viewModel.FigureBustRatio;
        sliderMatrix.TallRatio = (float)_viewModel.FigureTallRatio;
        sliderMatrix.EyeRatio = (float)_viewModel.FigureEyeRatio;
    }

    private void ApplyFigureSlidersAndRefreshPreview()
    {
        if (_loadedDocument is null)
        {
            return;
        }

        if (_viewModel.Morphs.Any(morph => morph.Ratio > 0.0))
        {
            ApplyMorphsToFigure();
        }
        else
        {
            ApplyFigureSlidersToFigure(_loadedDocument.Figure);
            _loadedDocument.Figure.UpdateBoneMatrices(true);
        }

        RefreshPreviewScene();
        SaveEditSessionSnapshot();
    }

    private void ApplyCurrentProportionsToFigure()
    {
        if (_loadedDocument is null)
        {
            return;
        }

        var figure = _loadedDocument.Figure;
        ApplyFigureSlidersToFigure(figure);

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

    private void ApplyMorphsToFigure()
    {
        if (_loadedDocument is null)
        {
            return;
        }

        ApplyCurrentProportionsToFigure();
        _morphing.Morph(_loadedDocument.Figure.Tmo);
        _loadedDocument.Figure.UpdateBoneMatricesWithoutTMOFrame();
    }

    private void ResetMorphRatios()
    {
        _isUpdatingMorphs = true;
        try
        {
            foreach (var morph in _viewModel.Morphs)
            {
                morph.Ratio = 0.0;
            }
        }
        finally
        {
            _isUpdatingMorphs = false;
        }
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
        var selectedRawVertexCount = _loadedDocument.SubMeshes
            .Where(item => _selectedSubMeshes[item.FlatIndex])
            .Sum(item => item.VertexCount);
        var vertexLimitWarning = selectedRawVertexCount >= 65535
            ? "，原始顶点数超过 65535，建议继续裁剪"
            : string.Empty;
        var scene = _viewModel.PreviewScene;

        if (scene is null || scene.IsEmpty)
        {
            _viewModel.PreviewSummary =
                $"已选择 {selectedCount}/{totalCount} 个子网格，原始顶点 {selectedRawVertexCount}，当前无可渲染三角形{vertexLimitWarning}。";
            return;
        }

        _viewModel.PreviewSummary =
            $"已选择 {selectedCount}/{totalCount} 个子网格，原始顶点 {selectedRawVertexCount}，预览顶点 {scene.Vertices.Count}，三角形 {scene.Indices.Count / 3}{vertexLimitWarning}。";
    }

    private async void OpenFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择 TSO 或 PNG 文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("TSO / PNG")
                {
                    Patterns = ["*.tso", "*.TSO", "*.png"]
                }
            ]
        }, RecentInputDirectoryKey);

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
        if (paths.Length > 0)
        {
            await LoadSourcesAsync(paths);
        }
    }

    private async void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanPickFolder)
        {
            _viewModel.StatusMessage = "当前平台不支持目录选择器。";
            return;
        }

        var folders = await OpenFolderPickerWithRecentAsync(new FolderPickerOpenOptions
        {
            Title = "选择包含 TSO 的目录",
            AllowMultiple = false
        }, RecentInputDirectoryKey);

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

        var folders = await OpenFolderPickerWithRecentAsync(new FolderPickerOpenOptions
        {
            Title = "选择输出目录",
            AllowMultiple = false
        }, RecentOutputDirectoryKey);

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _viewModel.CustomOutputFolder = path;
            _viewModel.OutputUseCustomFolder = true;
            SaveSettingsFromViewModel();
        }
    }

    private void RefreshPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ApplyFigureSlidersAndRefreshPreview();
        _viewModel.StatusMessage = "预览已刷新。";
    }

    private async void ExportPreviewImageButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.PreviewScene is null || _viewModel.PreviewScene.IsEmpty)
        {
            _viewModel.StatusMessage = "当前没有可导出的预览画面。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var pngType = CreatePngFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存预览 PNG",
            SuggestedFileName = $"{GetSourceBaseName(_loadedDocument.SourcePath)}_preview.png",
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                pngType
            ],
            SuggestedFileType = pngType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            var capture = await PreviewControl.CaptureFrameAsync();
            if (capture is null)
            {
                _viewModel.StatusMessage = "预览画面尚未初始化，无法导出。";
                return;
            }

            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(
                capture.RgbaPixels,
                capture.Width,
                capture.Height);
            await using var stream = await file.OpenWriteAsync();
            await image.SaveAsPngAsync(stream);

            _viewModel.StatusMessage = $"已导出预览 PNG：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出预览 PNG 失败：{ex.Message}";
        }
    }

    private void Window_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDroppedPaths(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_OnDrop(object? sender, DragEventArgs e)
    {
        if (TryGetDroppedPaths(e, out var paths))
        {
            if (paths.Length == 1 &&
                Path.GetExtension(paths[0]).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                ImportTpoConfig(paths[0]);
            }
            else if (paths.Length == 1 &&
                     Path.GetExtension(paths[0]).Equals(".tmo", StringComparison.OrdinalIgnoreCase))
            {
                await ImportTmoAsync(paths[0]);
            }
            else
            {
                await LoadSourcesAsync(paths);
            }
        }

        e.Handled = true;
    }

    private static bool TryGetDroppedPath(DragEventArgs e, out string path)
    {
        path = string.Empty;

        if (!TryGetDroppedPaths(e, out var paths) || paths.Length != 1)
        {
            return false;
        }

        path = paths[0];
        return true;
    }

    private static bool TryGetDroppedPaths(DragEventArgs e, out string[] paths)
    {
        paths = [];

        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return false;
        }

        paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
        if (paths.Length == 0)
        {
            return false;
        }

        if (paths.Length > 1)
        {
            return paths.All(IsTsoPath);
        }

        if (Directory.Exists(paths[0]))
        {
            return true;
        }

        var extension = Path.GetExtension(paths[0]);
        return extension.Equals(".tso", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tmo", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".vmd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTsoPath(string path)
        => Path.GetExtension(path).Equals(".tso", StringComparison.OrdinalIgnoreCase);

    private async void ImportTmoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择 TMO 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreateTmoFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await ImportTmoAsync(path);
        }
    }

    private async void ImportVmdButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择 VMD 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreateVmdFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await ImportVmdAsync(path);
        }
    }

    private async void ImportPosePngButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型，再导入姿势 PNG。";
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择 pose-only 姿势 PNG",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreatePngFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var pose = await Task.Run(() => _documentLoader.LoadPosePng(path));
            if (!TmoContainsFigureNodes(_loadedDocument.Figure, pose.Tmo, out var missingNode))
            {
                throw new InvalidOperationException($"姿势 PNG 缺少当前模型需要的骨骼：{missingNode}");
            }

            if (pose.LightDirection != Microsoft.DirectX.Vector3.Empty)
            {
                _loadedDocument.Figure.LightDirection = pose.LightDirection;
            }

            _loadedDocument.Figure.Tmo = pose.Tmo;
            _loadedDocument.Figure.UpdateNodeMapAndBoneMatrices();
            ResetMorphRatios();
            ApplyCurrentProportionsToFigure();
            RefreshPreviewScene();

            _viewModel.StatusMessage = $"已导入姿势 PNG：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导入姿势 PNG 失败：{ex.Message}";
        }
    }

    private async Task ImportTmoAsync(string path)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型，再导入 TMO。";
            return;
        }

        try
        {
            _viewModel.StatusMessage = $"正在导入 TMO：{path}";

            var tmo = await Task.Run(() =>
            {
                var loaded = new TMOFile();
                loaded.Load(path);
                return loaded;
            });

            if (tmo.nodes is null || tmo.nodes.Length == 0)
            {
                throw new InvalidOperationException("TMO 不包含骨骼节点。");
            }

            if (tmo.frames is null || tmo.frames.Length == 0)
            {
                throw new InvalidOperationException("TMO 不包含动作帧。");
            }

            if (!TmoContainsFigureNodes(_loadedDocument.Figure, tmo, out var missingNode))
            {
                throw new InvalidOperationException($"TMO 缺少当前模型需要的骨骼：{missingNode}");
            }

            _loadedDocument.Figure.Tmo = tmo;
            _loadedDocument.Figure.UpdateNodeMapAndBoneMatrices();
            ResetMorphRatios();
            ApplyCurrentProportionsToFigure();
            RefreshPreviewScene();

            _viewModel.StatusMessage = $"已导入 TMO：{path}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导入 TMO 失败：{ex.Message}";
        }
    }

    private async Task ImportVmdAsync(string path)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型，再导入 VMD。";
            return;
        }

        try
        {
            _viewModel.StatusMessage = $"正在转换 VMD：{path}";

            var tmo = await Task.Run(() =>
            {
                var vmd = VmdFile.Load(path);
                var converter = new VmdTmoConverter(Path.Combine(
                    _correspondTableList.GetSourcePath(),
                    "Girl2Miku_Default"));
                return converter.Convert(_loadedDocument.Figure, vmd, _morphing);
            });

            if (!TmoContainsFigureNodes(_loadedDocument.Figure, tmo, out var missingNode))
            {
                throw new InvalidOperationException($"VMD 转换结果缺少当前模型需要的骨骼：{missingNode}");
            }

            _loadedDocument.Figure.Tmo = tmo;
            _loadedDocument.Figure.UpdateNodeMapAndBoneMatrices();
            ResetMorphRatios();
            ApplyCurrentProportionsToFigure();
            RefreshPreviewScene();

            _viewModel.StatusMessage = $"已导入 VMD 为 TMO：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导入 VMD 失败：{ex.Message}";
        }
    }

    private async void ExportTmoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var tmoType = CreateTmoFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存当前预览 TMO",
            SuggestedFileName = $"{GetSourceBaseName(_loadedDocument.SourcePath)}.tmo",
            DefaultExtension = "tmo",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                tmoType
            ],
            SuggestedFileType = tmoType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            var figure = _loadedDocument.Figure;
            if (_viewModel.Morphs.Any(morph => morph.Ratio > 0.0))
            {
                ApplyMorphsToFigure();
            }
            else
            {
                ApplyCurrentProportionsToFigure();
            }

            using (var stream = await file.OpenWriteAsync())
            {
                figure.Tmo.Save(stream);
            }

            _viewModel.StatusMessage = $"已导出 TMO：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出 TMO 失败：{ex.Message}";
        }
    }

    private async void ExportVpdButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var vpdType = CreateVpdFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存当前姿势 VPD",
            SuggestedFileName = $"{GetSourceBaseName(_loadedDocument.SourcePath)}.vpd",
            DefaultExtension = "vpd",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                vpdType
            ],
            SuggestedFileType = vpdType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            if (_viewModel.Morphs.Any(morph => morph.Ratio > 0.0))
            {
                ApplyMorphsToFigure();
            }
            else
            {
                ApplyCurrentProportionsToFigure();
            }

            var correspondTableDirectory = Path.Combine(_correspondTableList.GetSourcePath(), "Girl2Miku_Default");
            await using (var stream = await file.OpenWriteAsync())
            {
                await Task.Run(() => VpdExporter.Save(
                    _loadedDocument.Figure,
                    correspondTableDirectory,
                    stream));
            }

            _viewModel.StatusMessage = $"已导出 VPD：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出 VPD 失败：{ex.Message}";
        }
    }

    private async void ExportPosePngButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var pngType = CreatePngFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存 pose-only 姿势 PNG",
            SuggestedFileName = $"{GetSourceBaseName(_loadedDocument.SourcePath)}_pose.png",
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                pngType
            ],
            SuggestedFileType = pngType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            if (_viewModel.Morphs.Any(morph => morph.Ratio > 0.0))
            {
                ApplyMorphsToFigure();
            }
            else
            {
                ApplyCurrentProportionsToFigure();
            }

            var previewPngBytes = await TryCapturePreviewPngAsync();
            await using var stream = await file.OpenWriteAsync();
            TdcgPngExporter.SavePose(
                _loadedDocument.Figure.Tmo,
                _loadedDocument.Figure.LightDirection,
                stream,
                previewPngBytes);

            _viewModel.StatusMessage = $"已导出姿势 PNG：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出姿势 PNG 失败：{ex.Message}";
        }
    }

    private async void ExportSelectedTsoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null)
        {
            _viewModel.StatusMessage = "请先选择 TSO。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var tsoType = CreateTsoFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存选中 TSO",
            SuggestedFileName = CreateTsoFileName(_viewModel.SelectedTsoFile),
            DefaultExtension = "tso",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                tsoType
            ],
            SuggestedFileType = tsoType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            SaveCurrentShaderEdits();
            using (var stream = await file.OpenWriteAsync())
            {
                _viewModel.SelectedTsoFile.Source.Tso.Save(stream);
            }

            _viewModel.StatusMessage = $"已导出 TSO：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出 TSO 失败：{ex.Message}";
        }
    }

    private async void ExportAllTsoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.TsoFiles.Count == 0)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        var folder = await PickOutputFolderAsync("选择 TSO 导出目录");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            SaveCurrentShaderEdits();
            foreach (var tsoFile in _viewModel.TsoFiles.OrderBy(item => item.Index))
            {
                tsoFile.Source.Tso.Save(Path.Combine(folder, CreateTsoFileName(tsoFile)));
            }

            _viewModel.StatusMessage = $"已导出 {_viewModel.TsoFiles.Count} 个 TSO：{folder}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出全部 TSO 失败：{ex.Message}";
        }
    }

    private async void ExportSelectedTsoTexturesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null)
        {
            _viewModel.StatusMessage = "请先选择 TSO。";
            return;
        }

        var folder = await PickOutputFolderAsync("选择贴图导出目录");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            var count = TsoTextureExporter.SaveTextures(
                _viewModel.SelectedTsoFile.Source.Tso,
                _viewModel.SelectedTsoFile.Index,
                _viewModel.SelectedTsoFile.Category,
                folder);

            _viewModel.StatusMessage = $"已导出 {count} 张贴图：{folder}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出贴图失败：{ex.Message}";
        }
    }

    private async void ExportAllTsoTexturesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        var folder = await PickOutputFolderAsync("选择贴图导出目录");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            var count = TsoTextureExporter.SaveAll(_loadedDocument, folder);
            _viewModel.StatusMessage = $"已导出 {count} 张贴图：{folder}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出全部贴图失败：{ex.Message}";
        }
    }

    private async void ExportSelectedSubScriptButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoSubScript is null)
        {
            _viewModel.StatusMessage = "请先选择 SubScript。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var textType = CreateTextFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存选中 SubScript",
            SuggestedFileName = CreateSubScriptFileName(_viewModel.SelectedTsoSubScript),
            DefaultExtension = "txt",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                textType
            ],
            SuggestedFileType = textType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            SaveCurrentShaderEdits();
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            foreach (var line in _viewModel.SelectedTsoSubScript.Source.lines)
            {
                await writer.WriteLineAsync(line);
            }

            _viewModel.StatusMessage = $"已导出 SubScript：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出 SubScript 失败：{ex.Message}";
        }
    }

    private async void ExportAllSubScriptsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        var folder = await PickOutputFolderAsync("选择 SubScript 导出目录");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            SaveCurrentShaderEdits();
            Directory.CreateDirectory(folder);

            var count = 0;
            for (var tsoIndex = 0; tsoIndex < _loadedDocument.Figure.TSOList.Count; tsoIndex++)
            {
                var tso = _loadedDocument.Figure.TSOList[tsoIndex];
                var category = _loadedDocument.Categories.Count > tsoIndex
                    ? _loadedDocument.Categories[tsoIndex]
                    : $"对象 {tsoIndex + 1}";

                for (var subScriptIndex = 0; subScriptIndex < tso.sub_scripts.Length; subScriptIndex++)
                {
                    var subScript = tso.sub_scripts[subScriptIndex];
                    var fileName =
                        $"{tsoIndex + 1:00}_{SanitizeFileName(category)}_{subScriptIndex + 1:00}_{SanitizeFileName(subScript.Name)}.txt";
                    var path = Path.Combine(folder, fileName);
                    await File.WriteAllLinesAsync(path, subScript.lines, new UTF8Encoding(false));
                    count++;
                }
            }

            _viewModel.StatusMessage = $"已导出 {count} 个 SubScript：{folder}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出全部 SubScript 失败：{ex.Message}";
        }
    }

    private async void ImportSelectedSubScriptButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null || _viewModel.SelectedTsoSubScript is null)
        {
            _viewModel.StatusMessage = "请先选择 SubScript。";
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择替换用 SubScript 文本",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreateSubScriptFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var selectedTsoIndex = _viewModel.SelectedTsoSubScript.TsoIndex;
        var selectedSubScriptIndex = _viewModel.SelectedTsoSubScript.Index;
        var subScript = _viewModel.SelectedTsoSubScript.Source;
        SaveCurrentShaderEdits();
        var oldLines = subScript.lines.ToArray();
        var oldFileName = subScript.FileName;
        var oldShader = subScript.shader;

        try
        {
            subScript.lines = LegacyEncoding.ReadAllLinesWithDetection(path);
            subScript.FileName = Path.GetFileName(path);
            subScript.GenerateShader();
            _ = subScript.shader.ColorTexName;
            _ = subScript.shader.ShadeTexName;

            RefreshAfterSubScriptChange(selectedTsoIndex, selectedSubScriptIndex);
            _viewModel.StatusMessage = $"已导入 SubScript：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            subScript.lines = oldLines;
            subScript.FileName = oldFileName;
            subScript.shader = oldShader;
            _viewModel.StatusMessage = $"导入 SubScript 失败：{ex.Message}";
        }
    }

    private async void ReplaceSelectedTsoTextureButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null || _viewModel.SelectedTsoTexture is null)
        {
            _viewModel.StatusMessage = "请先选择贴图。";
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择替换用贴图",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreateTextureFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            TsoTextureExporter.ReplaceTexture(_viewModel.SelectedTsoTexture.Source, path);
            _viewModel.SelectedTsoTexture.Refresh();
            UpdateTexturePreview(_viewModel.SelectedTsoTexture);
            RefreshPreviewScene();
            _viewModel.StatusMessage = $"已替换贴图：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"替换贴图失败：{ex.Message}";
        }
    }

    private async Task<string?> PickOutputFolderAsync(string title)
    {
        if (!StorageProvider.CanPickFolder)
        {
            _viewModel.StatusMessage = "当前平台不支持目录选择器。";
            return null;
        }

        var folders = await OpenFolderPickerWithRecentAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        }, RecentOutputDirectoryKey);

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private async void ExportTdcgPngButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (!StorageProvider.CanSave)
        {
            _viewModel.StatusMessage = "当前平台不支持保存文件选择器。";
            return;
        }

        var pngType = CreatePngFileType();
        var file = await SaveFilePickerWithRecentAsync(new FilePickerSaveOptions
        {
            Title = "保存当前 Figure TDCG PNG",
            SuggestedFileName = $"{GetSourceBaseName(_loadedDocument.SourcePath)}.png",
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                pngType
            ],
            SuggestedFileType = pngType
        }, RecentOutputDirectoryKey);

        if (file is null)
        {
            return;
        }

        try
        {
            SaveCurrentShaderEdits();
            if (_viewModel.Morphs.Any(morph => morph.Ratio > 0.0))
            {
                ApplyMorphsToFigure();
            }
            else
            {
                ApplyCurrentProportionsToFigure();
            }

            var previewPngBytes = await TryCapturePreviewPngAsync();
            using (var stream = await file.OpenWriteAsync())
            {
                TdcgPngExporter.Save(_loadedDocument, stream, previewPngBytes);
            }

            _viewModel.StatusMessage = $"已导出当前 Figure TDCG PNG：{file.TryGetLocalPath() ?? file.Name}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"导出当前 Figure TDCG PNG 失败：{ex.Message}";
        }
    }

    private static FilePickerFileType CreateTmoFileType()
        => new("TMO")
        {
            Patterns = ["*.tmo", "*.TMO"]
        };

    private static FilePickerFileType CreateVmdFileType()
        => new("VMD")
        {
            Patterns = ["*.vmd", "*.VMD"]
        };

    private static FilePickerFileType CreateVpdFileType()
        => new("VPD")
        {
            Patterns = ["*.vpd", "*.VPD"]
        };

    private async Task<byte[]?> TryCapturePreviewPngAsync()
    {
        try
        {
            var capture = await PreviewControl.CaptureFrameAsync();
            if (capture is null)
            {
                return null;
            }

            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(
                capture.RgbaPixels,
                capture.Width,
                capture.Height);
            await using var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static FilePickerFileType CreateTsoFileType()
        => new("TSO")
        {
            Patterns = ["*.tso", "*.TSO"]
        };

    private static FilePickerFileType CreatePngFileType()
        => new("PNG")
        {
            Patterns = ["*.png", "*.PNG"]
        };

    private static FilePickerFileType CreateTextureFileType()
        => new("Texture")
        {
            Patterns = ["*.bmp", "*.BMP", "*.tga", "*.TGA", "*.png", "*.PNG"]
        };

    private static FilePickerFileType CreateSubScriptFileType()
        => new("SubScript")
        {
            Patterns = ["*.txt", "*.TXT", "*.cgfx", "*.CGFX", "*.shader", "*.SHADER"]
        };

    private static FilePickerFileType CreateTextFileType()
        => new("Text")
        {
            Patterns = ["*.txt", "*.TXT"]
        };

    private static string CreateTsoFileName(TsoFileViewModel tsoFile)
        => $"{tsoFile.Index + 1:00}_{SanitizeFileName(tsoFile.Category)}.tso";

    private static string CreateSubScriptFileName(TsoSubScriptViewModel subScript)
        => $"{subScript.TsoIndex + 1:00}_{subScript.Index + 1:00}_{SanitizeFileName(subScript.Source.Name)}.txt";

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "TSO" : sanitized;
    }

    private static bool TmoContainsFigureNodes(Figure figure, TMOFile tmo, out string missingNode)
    {
        missingNode = string.Empty;

        if (tmo.nodemap is null)
        {
            missingNode = "nodemap";
            return false;
        }

        foreach (var tso in figure.TSOList)
        {
            foreach (var node in tso.nodes)
            {
                if (!tmo.nodemap.ContainsKey(node.Path))
                {
                    missingNode = node.Path;
                    return false;
                }
            }
        }

        return true;
    }

    private static void ValidateTsoCompatibleWithFigure(Figure figure, TSOFile tso, string sourcePath)
    {
        if (figure.Tmo.nodemap is null)
        {
            throw new InvalidOperationException("当前模型没有可用于追加校验的 TMO 骨骼映射。");
        }

        foreach (var node in tso.nodes)
        {
            if (!figure.Tmo.nodemap.ContainsKey(node.Path))
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(sourcePath)} 缺少当前模型 TMO 可匹配的骨骼：{node.Path}");
            }
        }
    }

    private static void SafeDisposeTso(TSOFile tso)
    {
        try
        {
            tso.Dispose();
        }
        catch (NullReferenceException)
        {
            // TSOFile.Dispose assumes a fully loaded file; ignore partial-load cleanup failures.
        }
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
            SaveCurrentShaderEdits();

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

        var config = CreateTpoConfigFromCurrentFigure();
        config.Save(Path.Combine(outputDirectory, "TPOConfig.xml"));

        var defaultConfigPath = LegacyPaths.Resolve("TPOConfig.xml");
        var defaultConfigDirectory = Path.GetDirectoryName(defaultConfigPath);
        if (!string.IsNullOrWhiteSpace(defaultConfigDirectory))
        {
            Directory.CreateDirectory(defaultConfigDirectory);
        }

        config.Save(defaultConfigPath);
    }

    private TPOConfig CreateTpoConfigFromCurrentFigure()
    {
        if (_loadedDocument is null)
        {
            return new TPOConfig();
        }

        return new TPOConfig
        {
            Proportions = _loadedDocument.Figure.TPOList.files
                .Select(file => new Proportion
                {
                    ClassName = file.ProportionName,
                    Ratio = file.Ratio
                })
                .ToArray()
        };
    }

    private async void ImportTpoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
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
        }, RecentInputDirectoryKey);

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

    private void ImportTpoConfig(string path)
    {
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

    private void SaveDefaultTpoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        try
        {
            ApplyCurrentProportionsToFigure();
            var path = LegacyPaths.Resolve("TPOConfig.xml");
            CreateTpoConfigFromCurrentFigure().Save(path);
            _viewModel.StatusMessage = $"已保存默认体型配置：{path}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"保存默认体型配置失败：{ex.Message}";
        }
    }

    private void ApplyProportionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (_viewModel.Morphs.Any(morph => morph.Ratio > 0.0))
        {
            ApplyMorphsToFigure();
        }
        else
        {
            ApplyCurrentProportionsToFigure();
        }

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

    private void FigureSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFigureSliders)
        {
            return;
        }

        ApplyFigureSlidersAndRefreshPreview();
    }

    private void MorphOptionViewModelOnPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MorphOptionViewModel.Ratio))
        {
            return;
        }

        if (_isUpdatingMorphs)
        {
            return;
        }

        ApplyMorphsToFigure();
        RefreshPreviewScene();
        _viewModel.StatusMessage = "表情预览已更新。";
    }

    private void ResetMorphsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ResetMorphRatios();
        ApplyMorphsToFigure();
        RefreshPreviewScene();
        _viewModel.StatusMessage = "表情预览已重置。";
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
        SaveSettingsFromViewModel();
    }

    private void FigureComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFigures ||
            _loadedSession is null ||
            _loadedDocument is null ||
            _viewModel.SelectedFigure is null ||
            _viewModel.SelectedFigure.Index == _loadedDocument.FigureIndex)
        {
            return;
        }

        try
        {
            var oldDocument = _loadedDocument;
            var document = _documentLoader.CreateDocument(_loadedSession, _viewModel.SelectedFigure.Index);
            _loadedDocument = document;
            oldDocument.Dispose();
            SetActiveDocument(document, _loadedSession.SourcePath, resetModelName: false);
            SaveEditSessionSnapshot();
            _viewModel.StatusMessage = $"已切换到 Figure {_viewModel.SelectedFigure.Index + 1}。";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"切换 Figure 失败：{ex.Message}";
        }
    }

    private async void AppendFigureFromPngButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedSession is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择要追加 Figure 的 TDCG PNG",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreatePngFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var importedSession = await Task.Run(() => _documentLoader.LoadSession(path));
            var figures = importedSession.DetachFigures();
            var firstAddedIndex = _loadedSession.Figures.Count;
            _loadedSession.AddFigures(figures);
            importedSession.Dispose();

            var oldDocument = _loadedDocument;
            var document = _documentLoader.CreateDocument(_loadedSession, firstAddedIndex);
            _loadedDocument = document;
            oldDocument?.Dispose();
            SetActiveDocument(document, _loadedSession.SourcePath, resetModelName: false);
            SaveEditSessionSnapshot();
            _viewModel.StatusMessage = $"已追加 {figures.Count} 个 Figure：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"追加 PNG Figure 失败：{ex.Message}";
        }
    }

    private void RemoveCurrentFigureButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedSession is null || _loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型。";
            return;
        }

        if (_loadedSession.Figures.Count <= 1)
        {
            _viewModel.StatusMessage = "至少需要保留 1 个 Figure。";
            return;
        }

        var removeIndex = _loadedDocument.FigureIndex;
        var oldDocument = _loadedDocument;
        var removed = _loadedSession.RemoveAt(removeIndex);
        var nextIndex = Math.Min(removeIndex, _loadedSession.Figures.Count - 1);

        try
        {
            var document = _documentLoader.CreateDocument(_loadedSession, nextIndex);
            _loadedDocument = document;
            oldDocument.Dispose();
            SetActiveDocument(document, _loadedSession.SourcePath, resetModelName: false);
            removed.Figure.Dispose();
            SaveEditSessionSnapshot();
            _viewModel.StatusMessage = $"已删除 Figure {removeIndex + 1}。";
        }
        catch (Exception ex)
        {
            _loadedSession.InsertAt(removeIndex, removed);
            _loadedDocument = oldDocument;
            _viewModel.StatusMessage = $"删除 Figure 失败：{ex.Message}";
        }
    }

    private void MoveTsoUpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedTso(-1);
    }

    private void MoveTsoDownButton_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedTso(1);
    }

    private async void AppendTsoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null)
        {
            _viewModel.StatusMessage = "请先加载模型，再追加 TSO。";
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择要追加的 TSO 文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                CreateTsoFileType()
            ]
        }, RecentInputDirectoryKey);

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        var loadedTsos = new List<(TSOFile Tso, string Category)>(paths.Length);
        var appendedToFigure = false;

        try
        {
            foreach (var path in paths)
            {
                var tso = new TSOFile();
                try
                {
                    tso.Load(path);
                    ValidateTsoCompatibleWithFigure(_loadedDocument.Figure, tso, path);
                    loadedTsos.Add((tso, Path.GetFileNameWithoutExtension(path)));
                }
                catch
                {
                    SafeDisposeTso(tso);
                    throw;
                }
            }

            var categories = _loadedDocument.Categories.ToList();
            var firstAddedIndex = _loadedDocument.Figure.TSOList.Count;

            foreach (var (tso, category) in loadedTsos)
            {
                _loadedDocument.Figure.TSOList.Add(tso);
                categories.Add(category);
            }

            appendedToFigure = true;
            RefreshCurrentDocument(categories, firstAddedIndex);
            _viewModel.StatusMessage = $"已追加 {loadedTsos.Count} 个 TSO。";
        }
        catch (Exception ex)
        {
            if (!appendedToFigure)
            {
                foreach (var (tso, _) in loadedTsos)
                {
                    SafeDisposeTso(tso);
                }
            }

            _viewModel.StatusMessage = $"追加 TSO 失败：{ex.Message}";
        }
    }

    private async void ReplaceSelectedTsoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null)
        {
            _viewModel.StatusMessage = "请先选择 TSO。";
            return;
        }

        if (!StorageProvider.CanOpen)
        {
            _viewModel.StatusMessage = "当前平台不支持文件选择器。";
            return;
        }

        var files = await OpenFilePickerWithRecentAsync(new FilePickerOpenOptions
        {
            Title = "选择替换用 TSO 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                CreateTsoFileType()
            ]
        }, RecentInputDirectoryKey);

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var replaceIndex = _viewModel.SelectedTsoFile.Index;
        if (replaceIndex < 0 || replaceIndex >= _loadedDocument.Figure.TSOList.Count)
        {
            return;
        }

        var replacement = new TSOFile();
        try
        {
            replacement.Load(path);
            ValidateTsoCompatibleWithFigure(_loadedDocument.Figure, replacement, path);

            var categories = _loadedDocument.Categories.ToList();
            while (categories.Count <= replaceIndex)
            {
                categories.Add($"对象 {categories.Count + 1}");
            }

            if (string.IsNullOrWhiteSpace(categories[replaceIndex]))
            {
                categories[replaceIndex] = Path.GetFileNameWithoutExtension(path);
            }

            var oldTso = _loadedDocument.Figure.TSOList[replaceIndex];
            _loadedDocument.Figure.TSOList[replaceIndex] = replacement;
            SafeDisposeTso(oldTso);

            RefreshCurrentDocument(categories, replaceIndex);
            _viewModel.StatusMessage = $"已替换 TSO：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            SafeDisposeTso(replacement);
            _viewModel.StatusMessage = $"替换 TSO 失败：{ex.Message}";
        }
    }

    private void RemoveSelectedTsoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null)
        {
            _viewModel.StatusMessage = "请先选择 TSO。";
            return;
        }

        if (_loadedDocument.Figure.TSOList.Count <= 1)
        {
            _viewModel.StatusMessage = "至少需要保留 1 个 TSO。";
            return;
        }

        var removeIndex = _viewModel.SelectedTsoFile.Index;
        if (removeIndex < 0 || removeIndex >= _loadedDocument.Figure.TSOList.Count)
        {
            return;
        }

        var categories = _loadedDocument.Categories.ToList();
        var removedCategory = categories.Count > removeIndex ? categories[removeIndex] : _viewModel.SelectedTsoFile.Category;
        var removedTso = _loadedDocument.Figure.TSOList[removeIndex];

        _loadedDocument.Figure.TSOList.RemoveAt(removeIndex);
        if (removeIndex < categories.Count)
        {
            categories.RemoveAt(removeIndex);
        }

        SafeDisposeTso(removedTso);

        var selectedIndex = Math.Min(removeIndex, _loadedDocument.Figure.TSOList.Count - 1);
        RefreshCurrentDocument(categories, selectedIndex);
        SaveEditSessionSnapshot();
        _viewModel.StatusMessage = $"已移除 TSO：{removedCategory}";
    }

    private void TsoFileListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
        PopulateTsoTextures(_viewModel.SelectedTsoFile);
        SaveEditSessionSnapshot();
    }

    private void TsoTextureListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateTexturePreview(_viewModel.SelectedTsoTexture);
    }

    private void TsoSubScriptListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedTsoSubScript is null)
        {
            return;
        }

        _viewModel.SelectedMaterial = _viewModel.Materials.FirstOrDefault(item =>
            item.Source.TsoIndex == _viewModel.SelectedTsoSubScript.TsoIndex &&
            item.Source.MaterialIndex == _viewModel.SelectedTsoSubScript.Index);
        UpdateMaterialDetails(_viewModel.SelectedMaterial);
    }

    private void UpdateTexturePreview(TsoTextureViewModel? texture)
    {
        if (texture is null)
        {
            _viewModel.TextureDetails = string.Empty;
            SetTexturePreview(null);
            return;
        }

        try
        {
            using var image = TsoTextureExporter.CreateImage(texture.Source);
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            SetTexturePreview(new Bitmap(stream));
            _viewModel.TextureDetails = texture.Details;
        }
        catch (Exception ex)
        {
            _viewModel.TextureDetails = $"贴图预览失败：{ex.Message}";
            SetTexturePreview(null);
        }
    }

    private void SetTexturePreview(Bitmap? preview)
    {
        var oldPreview = _viewModel.TexturePreview;
        _viewModel.TexturePreview = preview;
        oldPreview?.Dispose();
    }

    private void RefreshAfterSubScriptChange(int selectedTsoIndex, int selectedSubScriptIndex)
    {
        if (_loadedDocument is null)
        {
            return;
        }

        var categories = _viewModel.TsoFiles
            .OrderBy(item => item.Index)
            .Select(item => string.IsNullOrWhiteSpace(item.Category)
                ? (_loadedDocument.Categories.Count > item.Index ? _loadedDocument.Categories[item.Index] : $"对象 {item.Index + 1}")
                : item.Category)
            .ToList();

        _loadedDocument = _documentLoader.Rebuild(_loadedDocument, categories);
        _viewModel.DocumentSummary = BuildDocumentSummary(_loadedDocument);
        PopulateMaterials(_loadedDocument);
        PopulateTsoFiles(_loadedDocument);
        _viewModel.SelectedTsoFile = _viewModel.TsoFiles.FirstOrDefault(item => item.Index == selectedTsoIndex);
        PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
        PopulateTsoTextures(_viewModel.SelectedTsoFile);
        _viewModel.SelectedTsoSubScript = _viewModel.TsoSubScripts.FirstOrDefault(item => item.Index == selectedSubScriptIndex);
        if (_viewModel.SelectedTsoSubScript is not null)
        {
            _viewModel.SelectedMaterial = _viewModel.Materials.FirstOrDefault(item =>
                item.Source.TsoIndex == selectedTsoIndex &&
                item.Source.MaterialIndex == selectedSubScriptIndex);
            UpdateMaterialDetails(_viewModel.SelectedMaterial);
        }

        RebuildMeshGroups();
        RefreshPreviewScene();
    }

    private void MoveSelectedTso(int delta)
    {
        if (_loadedDocument is null || _viewModel.SelectedTsoFile is null)
        {
            return;
        }

        var oldIndex = _viewModel.SelectedTsoFile.Index;
        var newIndex = oldIndex + delta;
        if (newIndex < 0 || newIndex >= _loadedDocument.Figure.TSOList.Count)
        {
            return;
        }

        var categories = _loadedDocument.Categories.ToList();
        (_loadedDocument.Figure.TSOList[oldIndex], _loadedDocument.Figure.TSOList[newIndex]) =
            (_loadedDocument.Figure.TSOList[newIndex], _loadedDocument.Figure.TSOList[oldIndex]);
        (categories[oldIndex], categories[newIndex]) = (categories[newIndex], categories[oldIndex]);

        _loadedDocument.Figure.UpdateNodeMapAndBoneMatrices();
        RebuildLoadedDocument(categories);
        _viewModel.SelectedTsoFile = _viewModel.TsoFiles.FirstOrDefault(item => item.Index == newIndex);
        PopulateTsoSubScripts(_viewModel.SelectedTsoFile);
        SaveEditSessionSnapshot();
    }

    private void SelectAllMeshesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetAllMeshSelection(true);
    }

    private void ClearAllMeshesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetAllMeshSelection(false);
    }

    private void InvertMeshesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        for (var index = 0; index < _selectedSubMeshes.Length; index++)
        {
            _selectedSubMeshes[index] = !_selectedSubMeshes[index];
        }

        RebuildMeshGroups();
        RefreshPreviewScene();
        SaveEditSessionSnapshot();
    }

    private void SetAllMeshSelection(bool isSelected)
    {
        Array.Fill(_selectedSubMeshes, isSelected);
        RebuildMeshGroups();
        RefreshPreviewScene();
        SaveEditSessionSnapshot();
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
            DocumentKind.TsoFileSet => "多个 TSO 文件",
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
