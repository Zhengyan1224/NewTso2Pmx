using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Microsoft.DirectX;

[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>
{
    public float X;
    public float Y;
    public float Z;

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Empty => new(0.0f, 0.0f, 0.0f);

    public float Length()
        => MathF.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public void Normalize()
    {
        this = Normalize(this);
    }

    public static Vector3 Normalize(Vector3 value)
    {
        var source = value.ToNumerics();
        if (source == System.Numerics.Vector3.Zero)
        {
            return Empty;
        }

        source = System.Numerics.Vector3.Normalize(source);
        return FromNumerics(source);
    }

    public static float Dot(Vector3 left, Vector3 right)
        => System.Numerics.Vector3.Dot(left.ToNumerics(), right.ToNumerics());

    public static Vector3 Lerp(Vector3 min, Vector3 max, float ratio)
        => FromNumerics(System.Numerics.Vector3.Lerp(min.ToNumerics(), max.ToNumerics(), ratio));

    public static Vector3 CatmullRom(Vector3 value1, Vector3 value2, Vector3 value3, Vector3 value4, float amount)
    {
        var t = amount;
        var t2 = t * t;
        var t3 = t2 * t;

        var x = 0.5f * ((2.0f * value2.X) +
                        ((-value1.X + value3.X) * t) +
                        ((2.0f * value1.X - (5.0f * value2.X) + (4.0f * value3.X) - value4.X) * t2) +
                        ((-value1.X + (3.0f * value2.X) - (3.0f * value3.X) + value4.X) * t3));
        var y = 0.5f * ((2.0f * value2.Y) +
                        ((-value1.Y + value3.Y) * t) +
                        ((2.0f * value1.Y - (5.0f * value2.Y) + (4.0f * value3.Y) - value4.Y) * t2) +
                        ((-value1.Y + (3.0f * value2.Y) - (3.0f * value3.Y) + value4.Y) * t3));
        var z = 0.5f * ((2.0f * value2.Z) +
                        ((-value1.Z + value3.Z) * t) +
                        ((2.0f * value1.Z - (5.0f * value2.Z) + (4.0f * value3.Z) - value4.Z) * t2) +
                        ((-value1.Z + (3.0f * value2.Z) - (3.0f * value3.Z) + value4.Z) * t3));

        return new Vector3(x, y, z);
    }

    public static Vector3 TransformCoordinate(Vector3 value, Matrix transform)
    {
        var v = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(value.X, value.Y, value.Z, 1.0f), transform.ToNumerics());
        if (MathF.Abs(v.W) > float.Epsilon)
        {
            v /= v.W;
        }

        return new Vector3(v.X, v.Y, v.Z);
    }

    public bool Equals(Vector3 other)
        => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    public override bool Equals(object? obj)
        => obj is Vector3 other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z);

    public override string ToString()
        => $"{{X:{X}, Y:{Y}, Z:{Z}}}";

    public static Vector3 operator +(Vector3 left, Vector3 right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3 operator -(Vector3 left, Vector3 right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3 operator *(Vector3 value, float scalar)
        => new(value.X * scalar, value.Y * scalar, value.Z * scalar);

    public static Vector3 operator *(float scalar, Vector3 value)
        => value * scalar;

    public static Vector3 operator /(Vector3 value, float scalar)
        => new(value.X / scalar, value.Y / scalar, value.Z / scalar);

    public static bool operator ==(Vector3 left, Vector3 right)
        => left.Equals(right);

    public static bool operator !=(Vector3 left, Vector3 right)
        => !left.Equals(right);

    internal System.Numerics.Vector3 ToNumerics()
        => new(X, Y, Z);

    internal static Vector3 FromNumerics(System.Numerics.Vector3 value)
        => new(value.X, value.Y, value.Z);
}

[StructLayout(LayoutKind.Sequential)]
public struct Vector4 : IEquatable<Vector4>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Vector4 Empty => new(0.0f, 0.0f, 0.0f, 0.0f);

    public bool Equals(Vector4 other)
        => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    public override bool Equals(object? obj)
        => obj is Vector4 other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z, W);

    public static bool operator ==(Vector4 left, Vector4 right)
        => left.Equals(right);

    public static bool operator !=(Vector4 left, Vector4 right)
        => !left.Equals(right);

    internal System.Numerics.Vector4 ToNumerics()
        => new(X, Y, Z, W);

    internal static Vector4 FromNumerics(System.Numerics.Vector4 value)
        => new(value.X, value.Y, value.Z, value.W);
}

[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : IEquatable<Quaternion>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Quaternion Identity => new(0.0f, 0.0f, 0.0f, 1.0f);

    public void Multiply(Quaternion other)
    {
        this = this * other;
    }

    public static Quaternion Invert(Quaternion value)
        => FromNumerics(System.Numerics.Quaternion.Inverse(value.ToNumerics()));

    public static Quaternion RotationAxis(Vector3 axis, float angle)
        => FromNumerics(System.Numerics.Quaternion.CreateFromAxisAngle(axis.ToNumerics(), angle));

    public static Quaternion RotationMatrix(Matrix matrix)
        => FromNumerics(System.Numerics.Quaternion.CreateFromRotationMatrix(matrix.ToNumerics()));

    public static Quaternion Slerp(Quaternion min, Quaternion max, float ratio)
        => FromNumerics(System.Numerics.Quaternion.Slerp(min.ToNumerics(), max.ToNumerics(), ratio));

    public bool Equals(Quaternion other)
        => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    public override bool Equals(object? obj)
        => obj is Quaternion other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z, W);

    public static Quaternion operator *(Quaternion left, Quaternion right)
        => FromNumerics(System.Numerics.Quaternion.Multiply(left.ToNumerics(), right.ToNumerics()));

    public static bool operator ==(Quaternion left, Quaternion right)
        => left.Equals(right);

    public static bool operator !=(Quaternion left, Quaternion right)
        => !left.Equals(right);

    internal System.Numerics.Quaternion ToNumerics()
        => new(X, Y, Z, W);

    internal static Quaternion FromNumerics(System.Numerics.Quaternion value)
        => new(value.X, value.Y, value.Z, value.W);
}

[StructLayout(LayoutKind.Sequential)]
public struct Matrix : IEquatable<Matrix>
{
    public float M11;
    public float M12;
    public float M13;
    public float M14;
    public float M21;
    public float M22;
    public float M23;
    public float M24;
    public float M31;
    public float M32;
    public float M33;
    public float M34;
    public float M41;
    public float M42;
    public float M43;
    public float M44;

    public static Matrix Identity => FromNumerics(Matrix4x4.Identity);

    public void Multiply(Matrix other)
    {
        this = this * other;
    }

    public static Matrix Invert(Matrix matrix)
        => Matrix4x4.Invert(matrix.ToNumerics(), out var result) ? FromNumerics(result) : Identity;

    public static Matrix RotationQuaternion(Quaternion quaternion)
        => FromNumerics(Matrix4x4.CreateFromQuaternion(quaternion.ToNumerics()));

    public static Matrix RotationX(float angle)
        => FromNumerics(Matrix4x4.CreateRotationX(angle));

    public static Matrix RotationY(float angle)
        => FromNumerics(Matrix4x4.CreateRotationY(angle));

    public static Matrix RotationZ(float angle)
        => FromNumerics(Matrix4x4.CreateRotationZ(angle));

    public static Matrix RotationYawPitchRoll(float yaw, float pitch, float roll)
        => FromNumerics(Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll));

    public static Matrix Translation(float x, float y, float z)
        => FromNumerics(Matrix4x4.CreateTranslation(x, y, z));

    public static Matrix Translation(Vector3 translation)
        => Translation(translation.X, translation.Y, translation.Z);

    public static Matrix Scaling(float x, float y, float z)
        => FromNumerics(Matrix4x4.CreateScale(x, y, z));

    public static Matrix Scaling(Vector3 scaling)
        => Scaling(scaling.X, scaling.Y, scaling.Z);

    public static Matrix PerspectiveFovLH(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
        => FromNumerics(Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance));

    public static Matrix PerspectiveFovRH(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
        => FromNumerics(Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance));

    public static Matrix LookAtLH(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
        => FromNumerics(Matrix4x4.CreateLookAt(cameraPosition.ToNumerics(), cameraTarget.ToNumerics(), cameraUpVector.ToNumerics()));

    public static Matrix LookAtRH(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
        => LookAtLH(cameraPosition, cameraTarget, cameraUpVector);

    public bool Equals(Matrix other)
        => M11.Equals(other.M11) &&
           M12.Equals(other.M12) &&
           M13.Equals(other.M13) &&
           M14.Equals(other.M14) &&
           M21.Equals(other.M21) &&
           M22.Equals(other.M22) &&
           M23.Equals(other.M23) &&
           M24.Equals(other.M24) &&
           M31.Equals(other.M31) &&
           M32.Equals(other.M32) &&
           M33.Equals(other.M33) &&
           M34.Equals(other.M34) &&
           M41.Equals(other.M41) &&
           M42.Equals(other.M42) &&
           M43.Equals(other.M43) &&
           M44.Equals(other.M44);

    public override bool Equals(object? obj)
        => obj is Matrix other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(M11);
        hash.Add(M12);
        hash.Add(M13);
        hash.Add(M14);
        hash.Add(M21);
        hash.Add(M22);
        hash.Add(M23);
        hash.Add(M24);
        hash.Add(M31);
        hash.Add(M32);
        hash.Add(M33);
        hash.Add(M34);
        hash.Add(M41);
        hash.Add(M42);
        hash.Add(M43);
        hash.Add(M44);
        return hash.ToHashCode();
    }

    public static Matrix operator *(Matrix left, Matrix right)
        => FromNumerics(left.ToNumerics() * right.ToNumerics());

    public static bool operator ==(Matrix left, Matrix right)
        => left.Equals(right);

    public static bool operator !=(Matrix left, Matrix right)
        => !left.Equals(right);

    internal Matrix4x4 ToNumerics()
        => new(
            M11, M12, M13, M14,
            M21, M22, M23, M24,
            M31, M32, M33, M34,
            M41, M42, M43, M44);

    internal static Matrix FromNumerics(Matrix4x4 value)
        => new()
        {
            M11 = value.M11,
            M12 = value.M12,
            M13 = value.M13,
            M14 = value.M14,
            M21 = value.M21,
            M22 = value.M22,
            M23 = value.M23,
            M24 = value.M24,
            M31 = value.M31,
            M32 = value.M32,
            M33 = value.M33,
            M34 = value.M34,
            M41 = value.M41,
            M42 = value.M42,
            M43 = value.M43,
            M44 = value.M44,
        };
}
