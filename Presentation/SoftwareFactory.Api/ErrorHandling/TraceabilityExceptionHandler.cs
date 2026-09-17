using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SoftwareFactory.Api.Contracts.Artifacts;
using SoftwareFactory.Api.Resources;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Api.ErrorHandling;

/// <summary>
/// Turns the failures of the traceability module into ProblemDetails (estandar-backend.md §2): 422 with detail per
/// field when the content breaks its schema, 409 with the list of relations when a delete is blocked, 404 when the
/// artifact does not exist for this tenant.
/// </summary>
internal sealed class TraceabilityExceptionHandler(IProblemDetailsService problemDetails, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
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

    /// <summary>Each failure of the module has one honest status code; anything else is left to the next handler.</summary>
    private ProblemDetails? Describe(Exception exception)
    {
        if (exception is ArtifactValidationException validation)
        {
            return Validation(validation);
        }

        if (exception is ArtifactHasRelationsException blocked)
        {
            return Blocked(blocked);
        }

        if (exception is ArtifactNotFoundException)
        {
            return NotFound();
        }

        if (exception is ArtifactStateTransitionException transition)
        {
            return InvalidTransition(transition);
        }

        if (exception is ArtifactTypeUnknownException unknown)
        {
            return UnknownType(unknown);
        }

        if (exception is RelationIncompatibleException incompatible)
        {
            return Incompatible(incompatible);
        }

        if (exception is RelationCrossProjectException crossProject)
        {
            return CrossProject(crossProject);
        }

        if (exception is RelationSelfReferenceException self)
        {
            return SelfReference(self);
        }

        if (exception is RelationAlreadyExistsException duplicate)
        {
            return Duplicate(duplicate);
        }

        if (exception is RelationTypeUnknownException unknownRelation)
        {
            return UnknownRelationType(unknownRelation);
        }

        if (exception is RelationLevelsOutOfRangeException levels)
        {
            return LevelsOutOfRange(levels);
        }

        if (exception is RelationNotFoundException)
        {
            return RelationNotFound();
        }

        if (exception is DecisionNotFoundException)
        {
            return DecisionNotFound();
        }

        if (exception is DecisionNotPendingException settled)
        {
            return AlreadySettled(settled);
        }

        if (exception is DecisionRoleNotCompetentException notCompetent)
        {
            return NotCompetent(notCompetent);
        }

        if (exception is SelfRatificationException)
        {
            return SelfRatification();
        }

        if (exception is ArtifactTypeWithoutCompetentRoleException unmapped)
        {
            return WithoutCompetentRole(unmapped);
        }

        return exception is ArtifactSchemaUpgradeUnavailableException upgrade ? UpgradeUnavailable(upgrade) : null;
    }

    private ValidationProblemDetails Validation(ArtifactValidationException exception) =>
        new(exception.Errors
            .GroupBy(error => error.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal))
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["ArtifactContentInvalid"],
        };

    private ProblemDetails Blocked(ArtifactHasRelationsException exception)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = messages["ArtifactDeleteBlockedTitle"],
            Detail = messages["ArtifactDeleteBlocked", exception.Relations.Count],
        };

        // The list travels with the error so the person sees what to undo instead of guessing (HU-001 §4).
        problem.Extensions["relations"] = exception.Relations.Select(BlockingRelationResponse.From).ToList();

        return problem;
    }

    private ProblemDetails InvalidTransition(ArtifactStateTransitionException exception) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = messages["ArtifactStateTransitionTitle"],
        Detail = messages["ArtifactStateTransition", exception.From, exception.To],
    };

    private ProblemDetails NotFound() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = messages["Status404Title"],
        Detail = messages["ArtifactNotFound"],
    };

    private ValidationProblemDetails UnknownType(ArtifactTypeUnknownException exception) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["type"] = [messages["ArtifactTypeUnknown", exception.ArtifactType]] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["ArtifactContentInvalid"],
        };

    /// <summary>422 naming the rule that was violated and what the matrix does allow instead (HU-002 §2).</summary>
    private ValidationProblemDetails Incompatible(RelationIncompatibleException exception)
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["type"] = [messages["RelationIncompatible", exception.SourceType, exception.RelationType, exception.TargetType]],
        })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["RelationRefusedTitle"],
        };

        problem.Extensions["allowedTargets"] = exception.AllowedTargets;

        return problem;
    }

    private ValidationProblemDetails CrossProject(RelationCrossProjectException exception) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["targetId"] = [messages["RelationCrossProject"]] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["RelationRefusedTitle"],
            Detail = messages["RelationCrossProjectDetail", exception.SourceId, exception.TargetId],
        };

    private ValidationProblemDetails SelfReference(RelationSelfReferenceException exception) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["targetId"] = [messages["RelationSelfReference"]] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["RelationRefusedTitle"],
            Detail = messages["RelationSelfReferenceDetail", exception.ArtifactId],
        };

    private ValidationProblemDetails UnknownRelationType(RelationTypeUnknownException exception) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["type"] = [messages["RelationTypeUnknown", exception.RelationType]] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["RelationRefusedTitle"],
        };

    private ProblemDetails Duplicate(RelationAlreadyExistsException exception) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = messages["RelationDuplicateTitle"],
        Detail = messages["RelationDuplicate", exception.RelationType],
    };

    private ValidationProblemDetails LevelsOutOfRange(RelationLevelsOutOfRangeException exception) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["levels"] = [messages["RelationLevels", exception.MaxLevels]] })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["RelationRefusedTitle"],
        };

    private ProblemDetails RelationNotFound() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = messages["Status404Title"],
        Detail = messages["RelationNotFound"],
    };

    private ProblemDetails DecisionNotFound() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = messages["Status404Title"],
        Detail = messages["DecisionNotFound"],
    };

    private ProblemDetails AlreadySettled(DecisionNotPendingException exception) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = messages["DecisionNotPendingTitle"],
        Detail = messages["DecisionNotPending", exception.State],
    };

    /// <summary>403 and not 422: the note is fine, it is this caller who may not close it (HU-003 §4).</summary>
    private ProblemDetails NotCompetent(DecisionRoleNotCompetentException exception)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = messages["Status403Title"],
            Detail = messages["DecisionRoleNotCompetent", string.Join(", ", exception.CompetentRoles)],
        };

        problem.Extensions["competentRoles"] = exception.CompetentRoles;

        return problem;
    }

    private ProblemDetails SelfRatification()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = messages["Status403Title"],
            Detail = messages["DecisionSelfRatification"],
        };

        // The code travels so the caller can tell the two 403 of this endpoint apart (HU-003 §4).
        problem.Extensions["code"] = "self_ratification";

        return problem;
    }

    private ValidationProblemDetails WithoutCompetentRole(ArtifactTypeWithoutCompetentRoleException exception)
    {
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["artifactIds"] = [messages["ArtifactTypeWithoutCompetentRole", string.Join(", ", exception.ArtifactTypes)]],
        })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["ArtifactTypeWithoutCompetentRoleTitle"],
        };

        problem.Extensions["code"] = "artifact_type_without_competent_role";
        problem.Extensions["artifactTypes"] = exception.ArtifactTypes;

        return problem;
    }

    private ProblemDetails UpgradeUnavailable(ArtifactSchemaUpgradeUnavailableException exception) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = messages["ArtifactSchemaUpgradeTitle"],
        Detail = messages["ArtifactSchemaUpgrade", exception.ArtifactType, exception.FromVersion, exception.ToVersion],
    };
}
