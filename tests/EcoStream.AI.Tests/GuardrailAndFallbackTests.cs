using EcoStream.AI.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EcoStream.AI.Tests;

public class GuardrailAndFallbackTests
{
    [Theory]
    [InlineData(4, 3, 4, false)] // within ±1 → AI refinement accepted
    [InlineData(2, 3, 2, false)]
    [InlineData(1, 5, 5, true)]  // AI downplays a severe case → rules win
    [InlineData(5, 2, 5, false)] // AI more cautious → keep the more severe
    public void Guardrail_never_lets_ai_downplay_by_more_than_one_level(int ai, int rules, int expected, bool adjusted)
    {
        SeverityGuardrail.Reconcile(ai, rules).Should().Be((expected, adjusted));
    }

    [Fact]
    public async Task Failing_ai_step_falls_back_to_rules()
    {
        var result = await AiFallback.RunAsync("test", TimeSpan.FromSeconds(5), NullLogger.Instance,
            _ => throw new HttpRequestException("provider down"), _ => Task.FromResult("rules"), default);

        result.Should().Be("rules");
    }

    [Fact]
    public async Task Slow_ai_step_times_out_and_falls_back()
    {
        var result = await AiFallback.RunAsync("test", TimeSpan.FromMilliseconds(50), NullLogger.Instance,
            async ct => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return "ai"; }, _ => Task.FromResult("rules"), default);

        result.Should().Be("rules");
    }

    [Fact]
    public async Task Caller_cancellation_is_not_swallowed()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => AiFallback.RunAsync("test", TimeSpan.FromSeconds(5), NullLogger.Instance,
            ct => Task.FromCanceled<string>(ct), _ => Task.FromResult("rules"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
