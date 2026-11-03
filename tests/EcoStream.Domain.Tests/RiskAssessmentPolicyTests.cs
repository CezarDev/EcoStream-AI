using EcoStream.Domain.Alerts;
using FluentAssertions;

namespace EcoStream.Domain.Tests;

public class RiskAssessmentPolicyTests
{
    [Fact]
    public void Calm_conditions_are_observation_level()
    {
        RiskAssessmentPolicy.Assess(new RiskInputs(2, 20, 1012, 0, 0), out var factors).Value.Should().Be(1);
        factors.Should().BeEmpty();
    }

    [Fact]
    public void Heavy_rain_over_flood_zone_with_large_population_escalates()
    {
        var severity = RiskAssessmentPolicy.Assess(new RiskInputs(85, 60, 1000, 2, 70_000), out var factors);

        severity.Value.Should().Be(5); // 1 + rain(2) + flood(1) + population(1)
        factors.Should().HaveCount(3);
    }

    [Fact]
    public void Severity_is_capped_at_5()
    {
        RiskAssessmentPolicy.Assess(new RiskInputs(150, 150, 950, 5, 500_000), out _).Should().Be(SeverityLevel.Catastrophic);
    }
}
