using EcoStream.Application.Common;
using EcoStream.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace EcoStream.Api;

/// <summary>Maps application/domain exceptions to RFC 7807 problem details.</summary>
internal sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var result = exception switch
        {
            ValidationException ve => Results.ValidationProblem(
                ve.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            NotFoundException nf => Results.Problem(nf.Message, statusCode: StatusCodes.Status404NotFound),
            DomainException de => Results.Problem(de.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => null,
        };

        if (result is null) return false; // falls back to the default 500 problem details

        await result.ExecuteAsync(httpContext);
        return true;
    }
}
