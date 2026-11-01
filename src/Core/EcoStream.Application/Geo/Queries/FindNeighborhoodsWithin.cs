using EcoStream.Application.Abstractions;
using FluentValidation;
using MediatR;

namespace EcoStream.Application.Geo.Queries;

public sealed record FindNeighborhoodsWithinQuery(double Latitude, double Longitude, double RadiusKm)
    : IRequest<IReadOnlyList<NeighborhoodProximityDto>>;

public sealed class FindNeighborhoodsWithinValidator : AbstractValidator<FindNeighborhoodsWithinQuery>
{
    public FindNeighborhoodsWithinValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.RadiusKm).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

internal sealed class FindNeighborhoodsWithinHandler(IGeoQueries geo)
    : IRequestHandler<FindNeighborhoodsWithinQuery, IReadOnlyList<NeighborhoodProximityDto>>
{
    public Task<IReadOnlyList<NeighborhoodProximityDto>> Handle(FindNeighborhoodsWithinQuery request, CancellationToken cancellationToken) =>
        geo.FindNeighborhoodsWithinAsync(request.Latitude, request.Longitude, request.RadiusKm, cancellationToken);
}
