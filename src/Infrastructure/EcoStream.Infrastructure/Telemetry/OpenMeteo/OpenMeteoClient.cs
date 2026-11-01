using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EcoStream.Infrastructure.Telemetry.OpenMeteo;

public sealed record ProviderReading(DateTimeOffset MeasuredAt, double RainfallMmPerHour, double WindGustKmh, double PressureHpa);

public interface IOpenMeteoClient
{
    Task<ProviderReading> GetCurrentAsync(double latitude, double longitude, CancellationToken cancellationToken);
}

public sealed class OpenMeteoOptions
{
    public const string Section = "OpenMeteo";

    public Uri BaseUrl { get; set; } = new("https://api.open-meteo.com/");
}

/// <summary>
/// Typed client for https://open-meteo.com (free, no API key). Resilience (retry, circuit breaker,
/// timeouts) is applied by the HttpClient pipeline registered in DependencyInjection.
/// </summary>
internal sealed class OpenMeteoClient(HttpClient http) : IOpenMeteoClient
{
    public async Task<ProviderReading> GetCurrentAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={latitude}&longitude={longitude}&current=precipitation,wind_gusts_10m,surface_pressure&timezone=GMT");

        var response = await http.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Empty response from Open-Meteo.");

        return Map(response);
    }

    internal static ProviderReading Map(OpenMeteoResponse response)
    {
        var c = response.Current ?? throw new InvalidOperationException("Open-Meteo response has no 'current' block.");
        if (c.Interval <= 0) throw new InvalidOperationException($"Invalid interval {c.Interval}.");

        // 'precipitation' is the sum over the preceding 'interval' seconds (usually 900 s = 15 min); normalise to mm/h.
        var rainfallPerHour = Math.Round((c.Precipitation ?? 0) * 3600d / c.Interval, 2);

        var measuredAt = DateTimeOffset.ParseExact(c.Time, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        return new ProviderReading(measuredAt, rainfallPerHour, c.WindGusts10m ?? 0, c.SurfacePressure ?? 0);
    }
}

internal sealed record OpenMeteoResponse([property: JsonPropertyName("current")] OpenMeteoCurrent? Current);

internal sealed record OpenMeteoCurrent(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("interval")] int Interval,
    [property: JsonPropertyName("precipitation")] double? Precipitation,
    [property: JsonPropertyName("wind_gusts_10m")] double? WindGusts10m,
    [property: JsonPropertyName("surface_pressure")] double? SurfacePressure);
