using System.Text.Json;
using EcoStream.Infrastructure.Telemetry.OpenMeteo;
using FluentAssertions;

namespace EcoStream.Infrastructure.Tests;

public class OpenMeteoClientTests
{
    // Shape of a real https://api.open-meteo.com/v1/forecast?current=... response (trimmed).
    private const string Sample = """
        {
          "latitude": -30.05, "longitude": -51.17,
          "current_units": { "time": "iso8601", "interval": "seconds", "precipitation": "mm",
                             "wind_gusts_10m": "km/h", "surface_pressure": "hPa" },
          "current": { "time": "2026-11-01T12:15", "interval": 900, "precipitation": 3.2,
                       "wind_gusts_10m": 61.9, "surface_pressure": 1003.4 }
        }
        """;

    [Fact]
    public void Maps_15_minute_precipitation_to_mm_per_hour_in_utc()
    {
        var response = JsonSerializer.Deserialize<OpenMeteoResponse>(Sample)!;

        var reading = OpenMeteoClient.Map(response);

        reading.RainfallMmPerHour.Should().Be(12.8);
        reading.WindGustKmh.Should().Be(61.9);
        reading.PressureHpa.Should().Be(1003.4);
        reading.MeasuredAt.Should().Be(new DateTimeOffset(2026, 11, 1, 12, 15, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Missing_current_block_fails_loudly()
    {
        var act = () => OpenMeteoClient.Map(new OpenMeteoResponse(null));

        act.Should().Throw<InvalidOperationException>();
    }
}
