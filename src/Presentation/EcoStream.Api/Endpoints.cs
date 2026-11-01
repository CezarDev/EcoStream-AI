using EcoStream.Application.Geo.Queries;
using EcoStream.Application.Stations.Queries;
using EcoStream.Application.Telemetry.Commands;
using EcoStream.Application.Telemetry.Queries;
using MediatR;

namespace EcoStream.Api;

internal static class Endpoints
{
    public static IEndpointRouteBuilder MapEcoStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        var stations = api.MapGroup("/stations").WithTags("Stations");
        stations.MapGet("/", (ISender s, CancellationToken ct, bool onlyActive = true) => s.Send(new ListStationsQuery(onlyActive), ct));
        stations.MapGet("/{code}/readings", (string code, DateTimeOffset? from, DateTimeOffset? to, ISender s, CancellationToken ct) =>
        {
            var end = to ?? DateTimeOffset.UtcNow;
            return s.Send(new GetStationSeriesQuery(code, from ?? end.AddHours(-24), end), ct);
        });

        var telemetry = api.MapGroup("/telemetry").WithTags("Telemetry");
        telemetry.MapGet("/latest", (ISender s, CancellationToken ct) => s.Send(new GetLatestReadingsQuery(), ct));
        telemetry.MapPost("/", async (IngestTelemetryCommand command, ISender s, CancellationToken ct) =>
        {
            var result = await s.Send(command, ct);
            return result.Outcome == IngestOutcome.Duplicate ? Results.Ok(result) : Results.Accepted(value: result);
        });

        var geo = api.MapGroup("/geo").WithTags("Geo");
        geo.MapGet("/neighborhoods/near", (double lat, double lon, ISender s, CancellationToken ct, double radiusKm = 5) =>
            s.Send(new FindNeighborhoodsWithinQuery(lat, lon, radiusKm), ct));

        return app;
    }
}
