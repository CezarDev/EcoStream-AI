using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using FluentAssertions;

namespace EcoStream.Application.Tests;

public class RuleBasedPipelineTests
{
    private static readonly AnomalySnapshot Storm =
        new("POA-CENTRO", -30.033, -51.23, 85, 60, 1000, ["rainfall 85 mm/h"], DateTimeOffset.UtcNow);

    private static AffectedAreasIdentified Areas(params AffectedNeighborhood[] n) =>
        new(Guid.NewGuid(), Storm, n, n.Sum(x => x.Population ?? 0));

    [Fact]
    public async Task Decider_escalates_when_neighborhoods_are_in_flood_zone()
    {
        var risk = await new RuleBasedRiskDecider().AssessAsync(
            Areas(new("Centro Histórico", 39_000, 0, true), new("Cidade Baixa", 16_000, 900, false)), default);

        risk.Severity.Should().Be(5);
        risk.AssessedBy.Should().Be(RuleBasedRiskDecider.Name);
        risk.RecommendedActions.Should().Contain(a => a.Contains("Centro Histórico", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, new[] { "slack" })]
    [InlineData(3, new[] { "slack", "telegram" })]
    [InlineData(5, new[] { "slack", "telegram", "sms" })]
    public async Task Composer_fans_out_by_severity(int severity, string[] expectedChannels)
    {
        var risk = new RiskAssessed(Guid.NewGuid(), Storm, severity, "summary", ["Centro Histórico"], ["act"], "rules-v1");

        var messages = await new RuleBasedAlertComposer().ComposeAsync(risk, default);

        messages.Select(m => m.Channel).Should().BeEquivalentTo(expectedChannels);
        messages.Should().OnlyContain(m => m.ReadingId == risk.ReadingId && m.Severity == severity);
    }

    [Fact]
    public async Task Sms_body_fits_160_chars()
    {
        var many = Enumerable.Range(1, 30).Select(i => $"Bairro {i}").ToList();
        var risk = new RiskAssessed(Guid.NewGuid(), Storm, 5, "summary", many, ["act"], "rules-v1");

        var sms = (await new RuleBasedAlertComposer().ComposeAsync(risk, default)).Single(m => m.Channel == AlertChannels.Sms);

        sms.Body.Length.Should().BeLessThanOrEqualTo(160);
    }
}
