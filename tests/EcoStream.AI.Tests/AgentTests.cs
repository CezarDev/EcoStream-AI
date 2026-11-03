using EcoStream.AI.Agents;
using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EcoStream.AI.Tests;

public class AgentTests
{
    private static readonly IOptions<AiOptions> Options = Microsoft.Extensions.Options.Options.Create(new AiOptions
    {
        Provider = AiProvider.OpenAI, ChatModel = "fake-model", EmbeddingModel = "fake-embed",
    });

    [Fact]
    public async Task Risk_decider_maps_structured_output_and_drops_hallucinated_citations()
    {
        var kernel = FakeChatCompletion.KernelReturning("""
            ```json
            {"severity":5,"summary":"Chuva muito forte sobre área de inundação.","recommendedActions":["Abrir abrigos","Evacuar a orla"],"citedProtocolIds":["P1","P9"]}
            ```
            """, out var fake);
        var decider = new AiRiskDecider(new RuleBasedRiskDecider(), new DocsProtocolRetriever(), kernel, Options);

        var risk = await decider.AssessAsync(Scenarios.StormOverFloodZone(), default);

        risk.Severity.Should().Be(5);
        risk.AssessedBy.Should().Be("ai:fake-model");
        risk.RecommendedActions.Should().Equal("Abrir abrigos", "Evacuar a orla");
        risk.CitedProtocols.Should().ContainSingle("P9 does not exist and must be discarded");
        fake.Calls.Should().ContainSingle().Which.ToString().Should().NotBeNull();
    }

    [Fact]
    public async Task Risk_decider_guardrail_overrides_a_dangerous_downgrade()
    {
        var kernel = FakeChatCompletion.KernelReturning(
            """{"severity":1,"summary":"Sem risco.","recommendedActions":["Monitorar"],"citedProtocolIds":[]}""", out _);
        var decider = new AiRiskDecider(new RuleBasedRiskDecider(), new DocsProtocolRetriever(), kernel, Options);

        var risk = await decider.AssessAsync(Scenarios.StormOverFloodZone(), default); // rules say 5

        risk.Severity.Should().Be(5);
        risk.AssessedBy.Should().EndWith("+guardrail");
    }

    [Fact]
    public async Task Risk_decider_rejects_out_of_range_severity()
    {
        var kernel = FakeChatCompletion.KernelReturning(
            """{"severity":9,"summary":"x","recommendedActions":["y"],"citedProtocolIds":[]}""", out _);
        var decider = new AiRiskDecider(new RuleBasedRiskDecider(), new DocsProtocolRetriever(), kernel, Options);

        var act = () => decider.AssessAsync(Scenarios.StormOverFloodZone(), default);

        await act.Should().ThrowAsync<InvalidDataException>(); // → FallbackRiskDecider uses the rules
    }

    [Fact]
    public async Task Communicator_keeps_channel_selection_deterministic_and_enforces_sms_limit()
    {
        var longSms = new string('A', 300);
        var kernel = FakeChatCompletion.KernelReturning(
            $$"""{"slack":"Resumo para operações","telegram":null,"sms":"{{longSms}}"}""", out _);
        var composer = new AiAlertComposer(new RuleBasedAlertComposer(), kernel);
        var risk = new RiskAssessed(Guid.NewGuid(), Scenarios.Storm, 4, "Resumo", ["Centro Histórico"], ["Abrir abrigos"], "ai:fake");

        var messages = await composer.ComposeAsync(risk, default);

        messages.Select(m => m.Channel).Should().BeEquivalentTo(AlertChannels.All);
        messages.Single(m => m.Channel == AlertChannels.Slack).Body.Should().Be("Resumo para operações");
        messages.Single(m => m.Channel == AlertChannels.Sms).Body.Length.Should().Be(RuleBasedAlertComposer.SmsMaxLength);
        messages.Single(m => m.Channel == AlertChannels.Telegram).Body.Should().NotBeNullOrWhiteSpace("missing channel uses rule text");
    }

    [Fact]
    public async Task Communicator_ignores_channels_the_severity_does_not_allow()
    {
        var kernel = FakeChatCompletion.KernelReturning("""{"slack":"ops","telegram":"publico","sms":"sms indevido"}""", out _);
        var composer = new AiAlertComposer(new RuleBasedAlertComposer(), kernel);
        var risk = new RiskAssessed(Guid.NewGuid(), Scenarios.Storm, 2, "Resumo", [], ["Monitorar"], "ai:fake");

        var messages = await composer.ComposeAsync(risk, default);

        messages.Should().ContainSingle().Which.Channel.Should().Be(AlertChannels.Slack);
    }
}
