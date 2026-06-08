using System.IO;
using Microsoft.DirectX;
using NewTso2Pmx.Core.Infrastructure;

namespace TDCG.Extensions;

public static class BinaryWriterMethods
{
    public static void WriteCString(this BinaryWriter bw, string s)
    {
        foreach (byte value in LegacyEncoding.ShiftJis.GetBytes(s))
        {
            bw.Write(value);
        }

        bw.Write((byte)0);
    }

    public static void Write(this BinaryWriter bw, ref Vector3 v)
    {
        bw.Write(v.X);
        bw.Write(v.Y);
        bw.Write(v.Z);
    }

    public static void Write(this BinaryWriter bw, ref Vector4 v)
    {
        bw.Write(v.X);
        bw.Write(v.Y);
        bw.Write(v.Z);
        bw.Write(v.W);
    }

    public static void Write(this BinaryWriter bw, ref Matrix m)
    {
        bw.Write(m.M11);
        bw.Write(m.M12);
        bw.Write(m.M13);
        bw.Write(m.M14);
        bw.Write(m.M21);
        bw.Write(m.M22);
        bw.Write(m.M23);
        bw.Write(m.M24);
        bw.Write(m.M31);
        bw.Write(m.M32);
        bw.Write(m.M33);
        bw.Write(m.M34);
        bw.Write(m.M41);
        bw.Write(m.M42);
        bw.Write(m.M43);
        bw.Write(m.M44);
    }
}
