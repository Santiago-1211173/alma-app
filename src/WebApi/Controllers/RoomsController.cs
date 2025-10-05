using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Rooms;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "EmailVerified")]
[ApiController]
[Route("api/v1/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomsService _rooms;

    public RoomsController(IRoomsService rooms)
        => _rooms = rooms;

    [HttpGet]
    public async Task<ActionResult<PagedResult<RoomListItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? onlyActive = null,
        CancellationToken ct = default)
    {
        var result = await _rooms.SearchAsync(q, page, pageSize, onlyActive, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _rooms.GetByIdAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _rooms.CreateAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoomRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _rooms.UpdateAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _rooms.DeleteAsync(id, ct);
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
