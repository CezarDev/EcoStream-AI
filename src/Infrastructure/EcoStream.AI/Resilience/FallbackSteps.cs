using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcoStream.AI.Resilience;

/// <summary>Runs the AI step within a time budget; any failure (timeout, provider down, invalid JSON) falls back to the rules.</summary>
internal static partial class AiFallback
{
    public static async Task<T> RunAsync<T>(
        string step, TimeSpan timeout, ILogger logger,
        Func<CancellationToken, Task<T>> ai, Func<CancellationToken, Task<T>> rules, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await ai(cts.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogFallback(logger, step, ex.GetType().Name, ex.Message);
            return await rules(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI step {Step} failed ({ErrorType}: {Error}); using rule-based fallback")]
    private static partial void LogFallback(ILogger logger, string step, string errorType, string error);
}

internal sealed class FallbackAffectedAreaAnalyzer(
    IAffectedAreaAnalyzer ai, IAffectedAreaAnalyzer rules, IOptions<AiOptions> options, ILogger<FallbackAffectedAreaAnalyzer> logger)
    : IAffectedAreaAnalyzer
{
    public Task<AffectedAreasIdentified> AnalyzeAsync(AnomalyDetectedIntegrationEvent anomaly, CancellationToken cancellationToken) =>
        AiFallback.RunAsync("DataAnalyst", options.Value.StepTimeout, logger,
            ct => ai.AnalyzeAsync(anomaly, ct), ct => rules.AnalyzeAsync(anomaly, ct), cancellationToken);
}

internal sealed class FallbackRiskDecider(
    IRiskDecider ai, IRiskDecider rules, IOptions<AiOptions> options, ILogger<FallbackRiskDecider> logger) : IRiskDecider
{
    public Task<RiskAssessed> AssessAsync(AffectedAreasIdentified areas, CancellationToken cancellationToken) =>
        AiFallback.RunAsync("RiskDecider", options.Value.StepTimeout, logger,
            ct => ai.AssessAsync(areas, ct), ct => rules.AssessAsync(areas, ct), cancellationToken);
}

internal sealed class FallbackAlertComposer(
    IAlertComposer ai, IAlertComposer rules, IOptions<AiOptions> options, ILogger<FallbackAlertComposer> logger) : IAlertComposer
{
    public Task<IReadOnlyList<AlertDispatchRequested>> ComposeAsync(RiskAssessed risk, CancellationToken cancellationToken) =>
        AiFallback.RunAsync("Communicator", options.Value.StepTimeout, logger,
            ct => ai.ComposeAsync(risk, ct), ct => rules.ComposeAsync(risk, ct), cancellationToken);
}
