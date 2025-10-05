using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Availability;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availability;

    public AvailabilityController(IAvailabilityService availability)
        => _availability = availability;

    [HttpGet("rules")]
    public async Task<ActionResult<IEnumerable<StaffAvailabilityRuleDto>>> GetRules([FromQuery] Guid staffId, CancellationToken ct)
    {
        var result = await _availability.GetRulesAsync(staffId, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("rules/{staffId:guid}")]
    public async Task<IActionResult> CreateRule(Guid staffId, [FromBody] UpsertStaffAvailabilityRuleDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.CreateRuleAsync(staffId, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetRules), new { staffId }, result.Value);
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpsertStaffAvailabilityRuleDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.UpdateRuleAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var result = await _availability.DeleteRuleAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpGet("time-off")]
    public async Task<ActionResult<IEnumerable<StaffTimeOffDto>>> GetTimeOff([FromQuery] Guid staffId, CancellationToken ct)
    {
        var result = await _availability.GetTimeOffAsync(staffId, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("time-off/{staffId:guid}")]
    public async Task<IActionResult> CreateTimeOff(Guid staffId, [FromBody] UpsertStaffTimeOffDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.CreateTimeOffAsync(staffId, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetTimeOff), new { staffId }, result.Value);
    }

    [HttpPut("time-off/{id:guid}")]
    public async Task<IActionResult> UpdateTimeOff(Guid id, [FromBody] UpsertStaffTimeOffDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.UpdateTimeOffAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("time-off/{id:guid}")]
    public async Task<IActionResult> DeleteTimeOff(Guid id, CancellationToken ct)
    {
        var result = await _availability.DeleteTimeOffAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpGet("room-closures")]
    public async Task<ActionResult<IEnumerable<RoomClosureDto>>> GetRoomClosures([FromQuery] Guid roomId, CancellationToken ct)
    {
        var result = await _availability.GetRoomClosuresAsync(roomId, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("room-closures/{roomId:guid}")]
    public async Task<IActionResult> CreateRoomClosure(Guid roomId, [FromBody] UpsertRoomClosureDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.CreateRoomClosureAsync(roomId, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetRoomClosures), new { roomId }, result.Value);
    }

    [HttpPut("room-closures/{id:guid}")]
    public async Task<IActionResult> UpdateRoomClosure(Guid id, [FromBody] UpsertRoomClosureDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.UpdateRoomClosureAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("room-closures/{id:guid}")]
    public async Task<IActionResult> DeleteRoomClosure(Guid id, CancellationToken ct)
    {
        var result = await _availability.DeleteRoomClosureAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpPost("is-available")]
    public async Task<ActionResult<CheckAvailabilityResponse>> IsAvailable([FromBody] CheckAvailabilityRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.CheckAvailabilityAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("find-slots")]
    public async Task<ActionResult<IEnumerable<SlotDto>>> FindSlots([FromBody] FindSlotsRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _availability.FindSlotsAsync(body, ct);
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
