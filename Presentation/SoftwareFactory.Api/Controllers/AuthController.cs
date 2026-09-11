using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SoftwareFactory.Api.Contracts.Auth;
using SoftwareFactory.Api.Resources;
using SoftwareFactory.Api.Security;
using SoftwareFactory.Application.Common.Security;
using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Api.Controllers;

/// <summary>Endpoints of estandar-auth.md §3. Thin: maps HTTP to <see cref="IAuthService"/> and owns the refresh cookie.</summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService, IStringLocalizer<ApiMessages> messages, IOptions<AuthOptions> authOptions) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SessionResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await authService.LoginAsync(new LoginCommand(request.Email, request.Password), cancellationToken);

        if (session is null)
        {
            RefreshCookie.Delete(Response);
            return Unauthenticated(messages["InvalidCredentials"]);
        }

        RefreshCookie.Write(Response, session);
        return Ok(SessionResponse.From(session));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> RefreshAsync(CancellationToken cancellationToken)
    {
        var presented = RefreshCookie.Read(Request);
        var session = presented is null ? null : await authService.RefreshAsync(presented, cancellationToken);

        if (session is null)
        {
            RefreshCookie.Delete(Response);
            return Unauthenticated(messages["InvalidSession"]);
        }

        RefreshCookie.Write(Response, session);
        return Ok(SessionResponse.From(session));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(RefreshCookie.Read(Request), cancellationToken);
        RefreshCookie.Delete(Response);
        return NoContent();
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = await authService.ChangePasswordAsync(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);

        if (errors.Count == 0)
        {
            return NoContent();
        }

        if (errors.Contains(PasswordChangeError.IncorrectCurrentPassword))
        {
            return Problem(detail: messages["IncorrectCurrentPassword"], statusCode: StatusCodes.Status400BadRequest, title: messages["PasswordChangeFailedTitle"]);
        }

        var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["newPassword"] = [.. errors.Select(PolicyMessage)],
        };

        return new ObjectResult(new ValidationProblemDetails(fieldErrors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = messages["PasswordChangeFailedTitle"],
        })
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            ContentTypes = { "application/problem+json" },
        };
    }

    [HttpGet("me")]
    [ProducesResponseType<SessionUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionUserResponse>> MeAsync(CancellationToken cancellationToken)
    {
        var user = await authService.GetCurrentUserAsync(cancellationToken);

        return user is null ? Unauthenticated(messages["InvalidSession"]) : Ok(SessionUserResponse.From(user));
    }

    private string PolicyMessage(PasswordChangeError error) => error switch
    {
        PasswordChangeError.TooShort => messages["PasswordTooShort", authOptions.Value.MinimumPasswordLength],
        PasswordChangeError.TooCommon => messages["PasswordTooCommon"],
        _ => messages["PasswordChangeFailedTitle"],
    };

    private ObjectResult Unauthenticated(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized, title: messages["AuthenticationFailedTitle"]);
}
