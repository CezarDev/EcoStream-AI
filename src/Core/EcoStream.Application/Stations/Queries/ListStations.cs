using EcoStream.Application.Abstractions;
using MediatR;

namespace EcoStream.Application.Stations.Queries;

public sealed record ListStationsQuery(bool OnlyActive = true) : IRequest<IReadOnlyList<StationDto>>;

internal sealed class ListStationsHandler(IStationQueries queries) : IRequestHandler<ListStationsQuery, IReadOnlyList<StationDto>>
{
    public Task<IReadOnlyList<StationDto>> Handle(ListStationsQuery request, CancellationToken cancellationToken) =>
        queries.ListAsync(request.OnlyActive, cancellationToken);
}
