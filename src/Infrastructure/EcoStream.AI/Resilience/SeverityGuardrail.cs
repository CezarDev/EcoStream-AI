namespace EcoStream.AI.Resilience;

/// <summary>
/// Safety rule between the AI decision and the deterministic baseline: the AI may refine severity by
/// one level either way; a larger disagreement resolves to the MORE severe of the two (fail safe).
/// </summary>
public static class SeverityGuardrail
{
    public const int MaxDeviation = 1;

    public static (int Severity, bool Adjusted) Reconcile(int aiSeverity, int rulesSeverity) =>
        Math.Abs(aiSeverity - rulesSeverity) <= MaxDeviation
            ? (aiSeverity, false)
            : (Math.Max(aiSeverity, rulesSeverity), aiSeverity < rulesSeverity);
}
