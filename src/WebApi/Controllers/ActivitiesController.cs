using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Activities;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/v1/activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IActivitiesService _activities;

    public ActivitiesController(IActivitiesService activities)
        => _activities = activities;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ActivityListItemDto>>> Search(
        [FromQuery] Guid? roomId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _activities.SearchAsync(roomId, from, to, status, page, pageSize, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _activities.GetByIdAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityRequestDto body, CancellationToken ct)
    {
        var result = await _activities.CreateAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityRequestDto body, CancellationToken ct)
    {
        var result = await _activities.UpdateAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _activities.CancelAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var result = await _activities.CompleteAsync(id, ct);
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
