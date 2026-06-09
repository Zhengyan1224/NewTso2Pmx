namespace Tso2Pmd;

internal readonly struct VmdInterpolation
{
    private const float CoordinateScale = 127.0f;

    public VmdInterpolation(byte[]? data)
    {
        X = CreateCurve(data, 0);
        Y = CreateCurve(data, 1);
        Z = CreateCurve(data, 2);
        Rotation = CreateCurve(data, 3);
    }

    public VmdBezierCurve X { get; }

    public VmdBezierCurve Y { get; }

    public VmdBezierCurve Z { get; }

    public VmdBezierCurve Rotation { get; }

    private static VmdBezierCurve CreateCurve(byte[]? data, int channel)
    {
        if (data is null || data.Length < 16)
        {
            return VmdBezierCurve.Linear;
        }

        return VmdBezierCurve.Create(
            ReadCoordinate(data, channel),
            ReadCoordinate(data, channel + 4),
            ReadCoordinate(data, channel + 8),
            ReadCoordinate(data, channel + 12));
    }

    private static float ReadCoordinate(byte[] data, int index)
        => index < data.Length ? Math.Clamp(data[index] / CoordinateScale, 0.0f, 1.0f) : 0.0f;
}

internal readonly struct VmdBezierCurve
{
    private const float Epsilon = 0.00001f;

    private readonly float _x1;
    private readonly float _y1;
    private readonly float _x2;
    private readonly float _y2;

    private VmdBezierCurve(float x1, float y1, float x2, float y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    public static VmdBezierCurve Linear { get; } = new(20.0f / 127.0f, 20.0f / 127.0f, 107.0f / 127.0f, 107.0f / 127.0f);

    public static VmdBezierCurve Create(float x1, float y1, float x2, float y2)
    {
        if (MathF.Abs(x1 - y1) < Epsilon && MathF.Abs(x2 - y2) < Epsilon)
        {
            return Linear;
        }

        if (x2 < x1)
        {
            return Linear;
        }

        return new VmdBezierCurve(x1, y1, x2, y2);
    }

    public float Evaluate(float x)
    {
        x = Math.Clamp(x, 0.0f, 1.0f);
        if (x <= 0.0f || x >= 1.0f)
        {
            return x;
        }

        var low = 0.0f;
        var high = 1.0f;
        var t = x;
        for (var i = 0; i < 24; i++)
        {
            t = (low + high) * 0.5f;
            var sampledX = Cubic(t, _x1, _x2);
            if (sampledX < x)
            {
                low = t;
            }
            else
            {
                high = t;
            }
        }

        return Math.Clamp(Cubic(t, _y1, _y2), 0.0f, 1.0f);
    }

    private static float Cubic(float t, float p1, float p2)
    {
        var inverse = 1.0f - t;
        return (3.0f * inverse * inverse * t * p1) +
               (3.0f * inverse * t * t * p2) +
               (t * t * t);
    }
}
