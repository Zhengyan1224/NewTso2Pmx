using TDCG;

namespace NewTso2Pmx.Core.Loading;

public sealed record LoadedMaterialInfo(
    int TsoIndex,
    int MaterialIndex,
    string Category,
    string MaterialName,
    string ScriptFileName,
    Shader Shader);
