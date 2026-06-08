using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DirectX;

namespace Microsoft.DirectX.Direct3D;

public enum DeclarationType
{
    Float2,
    Float3,
    Float4,
    Ubyte4,
}

public enum DeclarationMethod
{
    Default,
}

public enum DeclarationUsage
{
    Position,
    Normal,
    TextureCoordinate,
}

public enum Usage
{
    None,
    Dynamic,
    WriteOnly,
}

public enum VertexFormats
{
    None,
}

public enum Pool
{
    Default,
    Managed,
}

public enum LockFlags
{
    None,
}

public readonly struct VertexElement
{
    public VertexElement(short stream, short offset, DeclarationType type, DeclarationMethod method, DeclarationUsage usage, byte usageIndex)
    {
        Stream = stream;
        Offset = offset;
        Type = type;
        Method = method;
        Usage = usage;
        UsageIndex = usageIndex;
    }

    public short Stream { get; }
    public short Offset { get; }
    public DeclarationType Type { get; }
    public DeclarationMethod Method { get; }
    public DeclarationUsage Usage { get; }
    public byte UsageIndex { get; }

    public static VertexElement VertexDeclarationEnd => new(0, 0, 0, 0, 0, 0);
}

public sealed class GraphicsStream : IDisposable
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _writer;

    public GraphicsStream()
    {
        _writer = new BinaryWriter(_stream);
    }

    public void Write(float value) => _writer.Write(value);

    public void Write(int value) => _writer.Write(value);

    public void Write(byte[] value) => _writer.Write(value);

    public void Write(Vector3 value)
    {
        _writer.Write(value.X);
        _writer.Write(value.Y);
        _writer.Write(value.Z);
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}

public sealed class Device : IDisposable
{
    public void Dispose()
    {
    }
}

public sealed class VertexBuffer : IDisposable
{
    public VertexBuffer(Type _, int __, Device device, Usage ___, VertexFormats ____, Pool _____)
    {
        Device = device;
    }

    public Device Device { get; }

    public event EventHandler? Created;

    public GraphicsStream Lock(int _, int __, LockFlags ___)
        => new();

    public void Unlock()
    {
    }

    public void RaiseCreated()
        => Created?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
    }
}

public sealed class Texture : IDisposable
{
    public void Dispose()
    {
    }
}

public static class TextureLoader
{
    public static Texture FromStream(Device _, Stream __)
        => new();
}

public sealed class EffectHandle
{
    public EffectHandle(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public readonly struct EffectDescription
{
    public EffectDescription(int techniques)
    {
        Techniques = techniques;
    }

    public int Techniques { get; }
}

public readonly struct TechniqueDescription
{
    public TechniqueDescription(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public sealed class Effect : IDisposable
{
    private readonly Dictionary<string, EffectHandle> _handles = new();

    public EffectDescription Description => new(0);

    public EffectHandle Technique { get; set; } = new("noop");

    public EffectHandle GetParameter(object? _, string name)
        => GetOrCreate(name);

    public EffectHandle GetTechnique(int index)
        => new($"technique_{index}");

    public TechniqueDescription GetTechniqueDescription(EffectHandle handle)
        => new(handle.Name);

    public void SetValue(string _, string __)
    {
    }

    public void SetValue(string _, float[] __)
    {
    }

    public void SetValue(EffectHandle _, Vector4 __)
    {
    }

    public void SetValue(EffectHandle _, Texture __)
    {
    }

    public void ValidateTechnique(EffectHandle _)
    {
    }

    public void Dispose()
    {
    }

    private EffectHandle GetOrCreate(string name)
    {
        if (!_handles.TryGetValue(name, out var handle))
        {
            handle = new EffectHandle(name);
            _handles[name] = handle;
        }

        return handle;
    }
}
