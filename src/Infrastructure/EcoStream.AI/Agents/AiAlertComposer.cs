using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using Microsoft.SemanticKernel;

namespace EcoStream.AI.Agents;

public sealed record ChannelTexts(string? Slack, string? Telegram, string? Sms);

/// <summary>
/// Agent 4 — Multichannel Communicator. It only writes the texts: which channels receive the alert
/// is decided deterministically by severity, and the SMS length is enforced after generation.
/// </summary>
public sealed class AiAlertComposer(RuleBasedAlertComposer rules, Kernel kernel) : IAlertComposer
{
    internal const string Instructions = """
        You are the Multichannel Communicator of a Civil Defense team in Brazil. Write alert texts in
        Brazilian Portuguese from the assessment provided, without adding facts.
        - slack: for the operations team; summary, affected neighborhoods, recommended actions as bullets.
        - telegram: for the public; clear, calm, 2-4 short sentences, what to do now, emergency phone 199.
        - sms: for the public; plain text without accents or emojis, at most 160 characters, include 199.
        Fill only the channels requested; others must be null. Respond with JSON only.
        """;

    public async Task<IReadOnlyList<AlertDispatchRequested>> ComposeAsync(RiskAssessed risk, CancellationToken cancellationToken)
    {
        var channels = RuleBasedAlertComposer.ChannelsFor(risk.Severity).ToList();
        if (channels.Count == 0) return [];

        var agent = AgentRunner.Create("Communicator", Instructions, kernel, useTools: false, responseFormat: typeof(ChannelTexts));
        var prompt = $"""
            Requested channels: {string.Join(", ", channels)}
            Assessment: {AgentRunner.ToJson(new { risk.Severity, risk.Summary, risk.AffectedNeighborhoods, risk.RecommendedActions, risk.Anomaly.StationCode })}
            """;
        var texts = AgentRunner.ParseJson<ChannelTexts>(await AgentRunner.RunAsync(agent, prompt, cancellationToken));

        // Any channel the model skipped falls back to the rule-based text for that channel only.
        var fallback = (await rules.ComposeAsync(risk, cancellationToken)).ToDictionary(m => m.Channel);
        var title = RuleBasedAlertComposer.TitleFor(risk);

        return channels.Select(channel =>
        {
            var body = channel switch
            {
                AlertChannels.Slack => texts.Slack,
                AlertChannels.Telegram => texts.Telegram,
                AlertChannels.Sms => texts.Sms is null ? null : RuleBasedAlertComposer.FitSms(texts.Sms.Trim()),
                _ => null,
            };
            return string.IsNullOrWhiteSpace(body)
                ? fallback[channel]
                : new AlertDispatchRequested(risk.ReadingId, risk.Severity, channel, title, body.Trim());
        }).ToList();
    }
}
