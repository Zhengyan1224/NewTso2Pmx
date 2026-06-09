using System.Collections.Generic;
using NewTso2Pmx.Core.Loading;

namespace NewTso2Pmx.App.Configuration;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public string Comment { get; set; } = "由 NewTso2Pmx 导出";

    public string OutputMode { get; set; } = AppOutputModes.CreateSubfolder;

    public string CustomOutputFolder { get; set; } = string.Empty;

    public bool UseHumanBone { get; set; } = true;

    public bool UseSpheremap { get; set; } = true;

    public bool UseEdge { get; set; }

    public bool UniqueMaterial { get; set; } = true;

    public bool EnableHairPhysics { get; set; } = true;

    public bool EnableChestPhysics { get; set; } = true;

    public bool EnableSkirtPhysics { get; set; } = true;

    public string? SelectedHairTemplate { get; set; }

    public string? SelectedChestTemplate { get; set; }

    public string? SelectedSkirtTemplate { get; set; }

    public MeshGroupingMode MeshGroupingMode { get; set; } = MeshGroupingMode.ByMaterial;

    public Dictionary<string, bool> BoneTableSelection { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, bool> ExtraPhysicsSelection { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> RecentDirectories { get; set; } = new(StringComparer.Ordinal);

    public EditSessionSettings EditSession { get; set; } = new();
}

public sealed class EditSessionSettings
{
    public string SourcePath { get; set; } = string.Empty;

    public int FigureIndex { get; set; }

    public int SelectedTsoIndex { get; set; }

    public List<string> TsoCategories { get; set; } = [];

    public List<bool> SelectedSubMeshes { get; set; } = [];
}

public static class AppOutputModes
{
    public const string CreateSubfolder = "CreateSubfolder";
    public const string SourceFolder = "SourceFolder";
    public const string CustomFolder = "CustomFolder";
}
