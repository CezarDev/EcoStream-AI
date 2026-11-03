using EcoStream.AI.Agents;
using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace EcoStream.AI.Tests;

/// <summary>Runs only when ECOSTREAM_AI_EVALS=1 (real provider, costs tokens). Configure with Ai__* env vars.</summary>
public sealed class AiEvalFactAttribute : FactAttribute
{
    public AiEvalFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ECOSTREAM_AI_EVALS") != "1")
            Skip = "AI evals disabled. Set ECOSTREAM_AI_EVALS=1 and Ai__Provider/Ai__ChatModel/Ai__EmbeddingModel/Ai__ApiKey.";
    }
}

/// <summary>
/// Behavioural evals of the real model against fixed scenarios. Protocol retrieval is deterministic
/// (docs on disk) so only the LLM's judgement is under test.
/// </summary>
public class AiEvalTests
{
    private static (Kernel Kernel, IOptions<AiOptions> Options) RealKernel()
    {
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var sp = new ServiceCollection().AddLogging().AddAiCore(config).BuildServiceProvider();
        return (sp.GetRequiredService<Kernel>(), sp.GetRequiredService<IOptions<AiOptions>>());
    }

    [AiEvalFact]
    public async Task Severe_storm_over_flood_zone_is_rated_high_with_cited_actions()
    {
        var (kernel, options) = RealKernel();
        var decider = new AiRiskDecider(new RuleBasedRiskDecider(), new DocsProtocolRetriever(), kernel, options);

        var risk = await decider.AssessAsync(Scenarios.StormOverFloodZone(), default);

        risk.Severity.Should().BeGreaterThanOrEqualTo(4);
        risk.AssessedBy.Should().NotContain("guardrail", "the model should agree with the baseline on an obvious case");
        risk.RecommendedActions.Should().NotBeEmpty();
        risk.CitedProtocols.Should().NotBeNullOrEmpty();
    }

    [AiEvalFact]
    public async Task Light_rain_with_nobody_exposed_stays_low()
    {
        var (kernel, options) = RealKernel();
        var decider = new AiRiskDecider(new RuleBasedRiskDecider(), new DocsProtocolRetriever(), kernel, options);

        var risk = await decider.AssessAsync(Scenarios.LightRainNobodyAround(), default);

        risk.Severity.Should().BeLessThanOrEqualTo(3);
    }

    [AiEvalFact]
    public async Task Public_texts_are_portuguese_actionable_and_within_limits()
    {
        var (kernel, _) = RealKernel();
        var composer = new AiAlertComposer(new RuleBasedAlertComposer(), kernel);
        var risk = new RiskAssessed(Guid.NewGuid(), Scenarios.Storm, 5, "Chuva de 85 mm/h sobre área de inundação no Centro.",
            ["Centro Histórico", "Cidade Baixa"], ["Abrir abrigos", "Evacuar a orla"], "eval");

        var messages = await composer.ComposeAsync(risk, default);

        var sms = messages.Single(m => m.Channel == AlertChannels.Sms).Body;
        sms.Length.Should().BeLessThanOrEqualTo(160);
        sms.Should().Contain("199");
        messages.Single(m => m.Channel == AlertChannels.Telegram).Body.Should().Contain("199");
    }
}
