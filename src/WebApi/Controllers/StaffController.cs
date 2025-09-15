using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Staff;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "EmailVerified")]
[ApiController]
[Route("api/v1/staff")]
public sealed class StaffController : ControllerBase
{
    private readonly AppDbContext _db;
    public StaffController(AppDbContext db) => _db = db;

    // GET /api/v1/staff?q=&page=&pageSize=
    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffListItemDto>>> Search([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var query = _db.Staff.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s =>
                EF.Functions.Like(s.FirstName, $"%{term}%") ||
                EF.Functions.Like(s.LastName,  $"%{term}%") ||
                EF.Functions.Like(s.Email,     $"%{term}%") ||
                EF.Functions.Like(s.Phone,     $"%{term}%") ||
                EF.Functions.Like(s.StaffNumber,$"%{term}%"));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new StaffListItemDto(s.Id, s.FirstName, s.LastName, s.Email, s.Phone, s.StaffNumber, s.Speciality))
            .ToListAsync();

        return Ok(PagedResult<StaffListItemDto>.Create(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var s = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        var dto = new StaffResponse(s.Id, s.FirstName, s.LastName, s.Email, s.Phone, s.StaffNumber, s.Speciality, s.CreatedAtUtc);
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var s = new Staff(body.FirstName, body.LastName, body.Email, body.Phone, body.StaffNumber, body.Speciality);
        _db.Staff.Add(s);
        await _db.SaveChangesAsync();

        var dto = new StaffResponse(s.Id, s.FirstName, s.LastName, s.Email, s.Phone, s.StaffNumber, s.Speciality, s.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStaffRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var s = await _db.Staff.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        s.Update(body.FirstName, body.LastName, body.Email, body.Phone, body.StaffNumber, body.Speciality);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var s = await _db.Staff.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        _db.Staff.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
