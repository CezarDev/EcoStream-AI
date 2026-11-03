namespace EcoStream.Domain.Alerts;

public sealed record RiskInputs(
    double RainfallMmPerHour,
    double WindGustKmh,
    double PressureHpa,
    int NeighborhoodsInFloodZone,
    int EstimatedPopulation);

/// <summary>
/// Deterministic baseline for severity. It is the fallback when the AI Risk Decider (Phase 3)
/// is unavailable, and the reference its answers are evaluated against.
/// </summary>
public static class RiskAssessmentPolicy
{
    public const int LargePopulation = 50_000;

    public static SeverityLevel Assess(RiskInputs inputs, out IReadOnlyList<string> factors)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var found = new List<string>();
        var score = 0;

        score += inputs.RainfallMmPerHour switch
        {
            >= 100 => Add(3, "extreme rainfall (≥100 mm/h)"),
            >= 80 => Add(2, "very heavy rainfall (≥80 mm/h)"),
            >= 50 => Add(1, "heavy rainfall (≥50 mm/h)"),
            _ => 0,
        };
        score += inputs.WindGustKmh switch
        {
            >= 120 => Add(2, "destructive gusts (≥120 km/h)"),
            >= 90 => Add(1, "strong gusts (≥90 km/h)"),
            _ => 0,
        };
        if (inputs.PressureHpa <= 970) score += Add(1, "deep low pressure (≤970 hPa)");
        if (inputs.NeighborhoodsInFloodZone > 0) score += Add(1, $"{inputs.NeighborhoodsInFloodZone} neighborhood(s) in mapped flood zones");
        if (inputs.EstimatedPopulation >= LargePopulation) score += Add(1, $"≥{LargePopulation:N0} people exposed");

        factors = found;
        return new SeverityLevel(Math.Clamp(1 + score, SeverityLevel.Min, SeverityLevel.Max));

        int Add(int points, string factor)
        {
            found.Add(factor);
            return points;
        }
    }
}
