using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Auth;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[ApiController]
[Route("me/onboarding")]
public class MeOnboardingController : ControllerBase
{
    private readonly IMeOnboardingService _onboarding;

    public MeOnboardingController(IMeOnboardingService onboarding)
        => _onboarding = onboarding;

    [HttpPost("client")]
    [Authorize(Policy = "EmailVerified")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientSelf body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _onboarding.CreateClientAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Created($"/api/v1/clients/{result.Value!.ClientId}", new { result.Value.ClientId });
    }

    [HttpPost("claim-staff")]
    [Authorize(Policy = "EmailVerified")]
    public async Task<IActionResult> ClaimStaff([FromBody] ClaimStaffBody body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _onboarding.ClaimStaffAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(new { message = result.Value!.Message, staffId = result.Value.StaffId });
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
