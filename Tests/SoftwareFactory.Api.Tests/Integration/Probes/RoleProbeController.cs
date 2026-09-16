using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SoftwareFactory.Api.Tests.Integration.Probes;

/// <summary>Test-only endpoints, one per policy, so the 401/403 behaviour of every role can be asserted without business endpoints.</summary>
[ApiController]
[Route("api/probe")]
public sealed class RoleProbeController : ControllerBase
{
    [HttpGet("any")]
    public IActionResult Any() => Ok(new { user = User.Identity?.Name });

    [HttpGet("admin")]
    [Authorize(Policy = "admin")]
    public IActionResult Admin() => Ok();

    [HttpGet("functional")]
    [Authorize(Policy = "functional")]
    public IActionResult Functional() => Ok();

    [HttpGet("architect")]
    [Authorize(Policy = "architect")]
    public IActionResult Architect() => Ok();

    [HttpGet("qa")]
    [Authorize(Policy = "qa")]
    public IActionResult Qa() => Ok();

    [HttpGet("compliance")]
    [Authorize(Policy = "compliance")]
    public IActionResult Compliance() => Ok();

    [HttpGet("reader")]
    [Authorize(Policy = "reader")]
    public IActionResult Reader() => Ok();
}
