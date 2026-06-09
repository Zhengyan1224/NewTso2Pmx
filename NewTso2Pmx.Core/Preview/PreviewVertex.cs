using System.Numerics;
using System.Runtime.InteropServices;

namespace NewTso2Pmx.Core.Preview;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct PreviewVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector4 Color,
    Vector2 TexCoord);
