using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Auth;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/v1/rbac")]
public class AdminRbacController : ControllerBase
{
    private readonly IAdminRbacService _rbac;

    public AdminRbacController(IAdminRbacService rbac)
        => _rbac = rbac;

    [HttpGet("users/{uid}/roles")]
    public async Task<IActionResult> GetRoles(string uid, CancellationToken ct)
    {
        var result = await _rbac.GetRolesAsync(uid, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("users/{uid}/roles")]
    public async Task<IActionResult> AssignRole(string uid, [FromBody] AssignRoleRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _rbac.AssignRoleAsync(uid, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetRoles), new { uid }, null);
    }

    [HttpDelete("users/{uid}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(string uid, RoleName role, CancellationToken ct)
    {
        var result = await _rbac.RemoveRoleAsync(uid, role, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    private ActionResult MapError(ServiceError error)
    {
        var problem = error.ToProblemDetails();

        return error.StatusCode switch
        {
            400 => BadRequest(problem),
            401 => Unauthorized(problem),
            404 => NotFound(problem),
            409 => Conflict(problem),
            _ => StatusCode(error.StatusCode, problem)
        };
    }
}
