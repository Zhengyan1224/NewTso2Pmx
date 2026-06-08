using System;
using TDCG;

namespace NewTso2Pmx.App.ViewModels;

public sealed class ShaderParameterViewModel : ViewModelBase
{
    public ShaderParameterViewModel(ShaderParameter source)
    {
        Source = source;
    }

    public ShaderParameter Source { get; }

    public string TypeName => Source.GetTypeName() ?? "unknown";

    public string Name
    {
        get => Source.Name;
        set
        {
            if (Source.Name == value)
            {
                return;
            }

            Source.Name = value;
            RaisePropertyChanged();
        }
    }

    public int Dimension => Source.Dimension;

    public bool HasF1 => Dimension >= 1;

    public bool HasF2 => Dimension >= 2;

    public bool HasF3 => Dimension >= 3;

    public bool HasF4 => Dimension >= 4;

    public bool HasTextValue => Dimension == 0;

    public string ValueText
    {
        get => Source.GetValueString() ?? string.Empty;
        set
        {
            if (Dimension > 0)
            {
                return;
            }

            if (TypeName == "texture")
            {
                Source.SetTexture(value);
            }
            else
            {
                Source.SetString(value);
            }

            RaisePropertyChanged();
        }
    }

    public double F1
    {
        get => Source.F1;
        set => SetFloatValue(Source.F1, value, parsed => Source.F1 = parsed, nameof(F1));
    }

    public double F2
    {
        get => Source.F2;
        set => SetFloatValue(Source.F2, value, parsed => Source.F2 = parsed, nameof(F2));
    }

    public double F3
    {
        get => Source.F3;
        set => SetFloatValue(Source.F3, value, parsed => Source.F3 = parsed, nameof(F3));
    }

    public double F4
    {
        get => Source.F4;
        set => SetFloatValue(Source.F4, value, parsed => Source.F4 = parsed, nameof(F4));
    }

    private void SetFloatValue(float current, double value, Action<float> setter, string propertyName)
    {
        var parsed = (float)value;
        if (Math.Abs(current - parsed) < 0.000001f)
        {
            return;
        }

        setter(parsed);
        RaisePropertyChanged(propertyName);
        RaisePropertyChanged(nameof(ValueText));
    }
}
