using EcoStream.Application.Abstractions;
using FluentValidation;
using MediatR;

namespace EcoStream.Application.Telemetry.Queries;

public sealed record GetLatestReadingsQuery : IRequest<IReadOnlyList<ReadingDto>>;

internal sealed class GetLatestReadingsHandler(ITelemetryQueries queries)
    : IRequestHandler<GetLatestReadingsQuery, IReadOnlyList<ReadingDto>>
{
    public Task<IReadOnlyList<ReadingDto>> Handle(GetLatestReadingsQuery request, CancellationToken cancellationToken) =>
        queries.GetLatestPerStationAsync(cancellationToken);
}

public sealed record GetStationSeriesQuery(string StationCode, DateTimeOffset From, DateTimeOffset To)
    : IRequest<IReadOnlyList<ReadingDto>>;

public sealed class GetStationSeriesValidator : AbstractValidator<GetStationSeriesQuery>
{
    public static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    public GetStationSeriesValidator()
    {
        RuleFor(x => x.StationCode).NotEmpty();
        RuleFor(x => x).Must(x => x.From < x.To).WithMessage("'From' must be before 'To'.")
            .Must(x => x.To - x.From <= MaxWindow).WithMessage($"Window cannot exceed {MaxWindow.TotalDays} days.");
    }
}

internal sealed class GetStationSeriesHandler(ITelemetryQueries queries)
    : IRequestHandler<GetStationSeriesQuery, IReadOnlyList<ReadingDto>>
{
    public Task<IReadOnlyList<ReadingDto>> Handle(GetStationSeriesQuery request, CancellationToken cancellationToken) =>
        queries.GetSeriesAsync(request.StationCode.ToUpperInvariant(), request.From.ToUniversalTime(), request.To.ToUniversalTime(), cancellationToken);
}
