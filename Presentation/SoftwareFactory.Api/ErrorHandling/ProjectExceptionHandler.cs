using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SoftwareFactory.Api.Resources;
using SoftwareFactory.Application.Project.Contracts;

namespace SoftwareFactory.Api.ErrorHandling;

/// <summary>
/// Turns the failures of the project module into ProblemDetails (estandar-backend.md §2): 409 with the name when
/// the tenant already has one, 404 when the project is not this tenant's, 422 when a field does not hold up.
/// </summary>
internal sealed class ProjectExceptionHandler(IProblemDetailsService problemDetails, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = Describe(exception);

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status400BadRequest;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private ProblemDetails? Describe(Exception exception) => exception switch
    {
        ProjectNameTakenException taken => NameTaken(taken),
        ProjectNotFoundException => NotFound(),
        ProjectValidationException invalid => Invalid(invalid),
        _ => null,
    };

    /// <summary>409 naming the project that is already there, so the person renames instead of guessing.</summary>
    private ProblemDetails NameTaken(ProjectNameTakenException exception)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = messages["ProjectNameTakenTitle"],
            Detail = messages["ProjectNameTaken", exception.Name],
        };

        problem.Extensions["name"] = exception.Name;

        return problem;
    }

    private ProblemDetails NotFound() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = messages["Status404Title"],
        Detail = messages["ProjectNotFound"],
    };

    private ValidationProblemDetails Invalid(ProjectValidationException exception) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { [exception.Field] = [exception.Message] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["ValidationFailedTitle"],
        };
}
