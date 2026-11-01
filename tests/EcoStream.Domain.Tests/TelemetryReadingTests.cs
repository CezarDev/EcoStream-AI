using EcoStream.Domain.Common;
using EcoStream.Domain.Geo;
using EcoStream.Domain.Telemetry;
using FluentAssertions;

namespace EcoStream.Domain.Tests;

public class TelemetryReadingTests
{
    private static readonly GeoPoint PortoAlegre = new(-30.05, -51.17);

    [Fact]
    public void Normal_reading_raises_no_anomaly()
    {
        var reading = TelemetryReading.Record("A801", PortoAlegre, DateTimeOffset.UtcNow, 4, 20, 1012, AnomalyThresholds.Default);

        reading.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Heavy_rain_and_gust_raise_severe_anomaly_with_reasons()
    {
        var reading = TelemetryReading.Record("A801", PortoAlegre, DateTimeOffset.UtcNow, 72, 104, 1000, AnomalyThresholds.Default);

        var evt = reading.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SevereAnomalyDetected>().Subject;
        evt.Reasons.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void GeoPoint_rejects_out_of_range_coordinates(double lat, double lon)
    {
        var act = () => new GeoPoint(lat, lon);

        act.Should().Throw<DomainException>();
    }
}
