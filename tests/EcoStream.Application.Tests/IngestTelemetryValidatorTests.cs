using EcoStream.Application.Telemetry.Commands;
using FluentAssertions;

namespace EcoStream.Application.Tests;

public class IngestTelemetryValidatorTests
{
    private readonly IngestTelemetryValidator _validator = new();

    [Fact]
    public void Rejects_negative_rainfall_and_empty_station()
    {
        var cmd = new IngestTelemetryCommand("", DateTimeOffset.UtcNow, -1, 10, 1010);

        var result = _validator.Validate(cmd);

        result.Errors.Select(e => e.PropertyName).Should().Contain(["StationCode", "RainfallMmPerHour"]);
    }

    [Fact]
    public void Rejects_readings_from_the_future()
    {
        var cmd = new IngestTelemetryCommand("A801", DateTimeOffset.UtcNow.AddHours(1), 0, 10, 1010);

        _validator.Validate(cmd).Errors.Should().ContainSingle(e => e.PropertyName == "MeasuredAt");
    }
}
