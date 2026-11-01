using EcoStream.Domain.Common;

namespace EcoStream.Domain.Alerts;

/// <summary>Risk severity on a 1 (observation) to 5 (catastrophic) scale.</summary>
public readonly record struct SeverityLevel
{
    public const int Min = 1;
    public const int Max = 5;

    public SeverityLevel(int value)
    {
        if (value is < Min or > Max) throw new DomainException($"Severity must be between {Min} and {Max}.");
        Value = value;
    }

    public int Value { get; }

    public static SeverityLevel Observation => new(1);
    public static SeverityLevel Catastrophic => new(5);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
