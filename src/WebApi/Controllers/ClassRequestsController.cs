using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.ClassRequests;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/class-requests")]
public sealed class ClassRequestsController : ControllerBase
{
    private readonly IClassRequestsService _classRequests;

    public ClassRequestsController(IClassRequestsService classRequests)
        => _classRequests = classRequests;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassRequestListItemDto>>> Search(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? staffId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? roomId,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _classRequests.SearchAsync(clientId, staffId, roomId, from, to, status, page, pageSize, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _classRequests.GetByIdAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "Staff")]
    public async Task<IActionResult> CreateForClient(
        [FromBody] CreateClassRequestByStaff body,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _classRequests.CreateForClientAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });
    }

    [HttpGet("me/client")]
    [Authorize(Policy = "Client")]
    public async Task<IActionResult> MyClientRequests(CancellationToken ct)
    {
        var result = await _classRequests.GetMyClientRequestsAsync(ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _classRequests.UpdateAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _classRequests.DeleteAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _classRequests.ApproveAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var result = await _classRequests.RejectAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
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
