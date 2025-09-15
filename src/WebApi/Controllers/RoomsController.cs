using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Rooms;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "EmailVerified")]
[ApiController]
[Route("api/v1/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    public RoomsController(AppDbContext db) => _db = db;

    // GET /api/v1/rooms?q=&page=&pageSize=&onlyActive=
    [HttpGet]
    public async Task<ActionResult<PagedResult<RoomListItemDto>>> Search([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool? onlyActive = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var query = _db.Rooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r => EF.Functions.Like(r.Name, $"%{term}%"));
        }
        if (onlyActive is true) query = query.Where(r => r.IsActive);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new RoomListItemDto(r.Id, r.Name, r.Capacity, r.IsActive))
            .ToListAsync();

        return Ok(PagedResult<RoomListItemDto>.Create(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var r = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        var dto = new RoomResponse(r.Id, r.Name, r.Capacity, r.IsActive, r.CreatedAtUtc);
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var r = new Room(body.Name, body.Capacity, body.IsActive);
        _db.Rooms.Add(r);
        await _db.SaveChangesAsync();

        var dto = new RoomResponse(r.Id, r.Name, r.Capacity, r.IsActive, r.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = r.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoomRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var r = await _db.Rooms.FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();

        r.Update(body.Name, body.Capacity, body.IsActive);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var r = await _db.Rooms.FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();

        _db.Rooms.Remove(r);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
