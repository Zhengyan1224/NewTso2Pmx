using Microsoft.DirectX;
using TDCG;

namespace NewTso2Pmx.Core.Loading;

public sealed class TdcgPoseInfo
{
    public TdcgPoseInfo(TMOFile tmo, Vector3 lightDirection)
    {
        Tmo = tmo;
        LightDirection = lightDirection;
    }

    public TMOFile Tmo { get; }

    public Vector3 LightDirection { get; }
}
