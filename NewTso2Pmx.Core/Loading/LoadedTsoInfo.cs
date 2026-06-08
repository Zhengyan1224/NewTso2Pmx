using TDCG;

namespace NewTso2Pmx.Core.Loading;

public sealed record LoadedTsoInfo(
    int Index,
    string Category,
    string SourceName,
    TSOFile Tso);
