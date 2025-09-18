using AlmaApp.Domain.Staff;
using AlmaApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("admin/staff")]
public class AdminStaffController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminStaffController(AppDbContext db) => _db = db;

    // DTOs
    public record CreateStaffRequest(
        string FirstName,
        string LastName,
        string Email,
        string Phone,
        string StaffNumber,
        string? Speciality
    );

    public record StaffDto(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string Phone,
        string StaffNumber,
        string? Speciality,
        string? FirebaseUid
    );

    public record LinkFirebaseRequest(string Uid);

    [HttpPost]
    public async Task<ActionResult<StaffDto>> Create([FromBody] CreateStaffRequest body)
    {
        var s = new Staff(
            firstName:   body.FirstName.Trim(),
            lastName:    body.LastName.Trim(),
            email:       body.Email.Trim().ToLowerInvariant(),
            phone:       body.Phone.Trim(),
            staffNumber: body.StaffNumber.Trim(),
            speciality:  string.IsNullOrWhiteSpace(body.Speciality) ? null : body.Speciality.Trim()
        );

        _db.Staff.Add(s);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = s.Id }, ToDto(s));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDto>> GetById(Guid id)
    {
        var s = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        return ToDto(s);
    }

    [HttpPost("{id:guid}/link")]
    public async Task<IActionResult> LinkFirebase(Guid id, [FromBody] LinkFirebaseRequest body)
    {
        var s = await _db.Staff.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        // garantir que o UID não está já ligado a outro staff
        var exists = await _db.Staff.AnyAsync(x => x.FirebaseUid == body.Uid && x.Id != id);
        if (exists)
            return Conflict(new ProblemDetails { Title = "UID já ligado a outro Staff", Status = 409 });

        s.LinkFirebase(body.Uid); // método que adicionaste no domínio
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static StaffDto ToDto(Staff s) =>
        new(
            s.Id,
            s.FirstName,
            s.LastName,
            s.Email,
            s.Phone,
            s.StaffNumber,
            s.Speciality,
            s.FirebaseUid
        );
}
