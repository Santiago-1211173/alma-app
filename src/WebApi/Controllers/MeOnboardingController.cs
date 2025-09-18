// Controllers/MeOnboardingController.cs
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[ApiController]
[Route("me/onboarding")]
public class MeOnboardingController(AppDbContext db, IUserContext user) : ControllerBase
{
    // Cliente self-service
    [HttpPost("client")]
    [Authorize(Policy = "EmailVerified")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientSelf body, CancellationToken ct)
    {
        // já existe?
        var exists = await db.Clients.AnyAsync(c =>
            (user.Uid   != null && c.FirebaseUid == user.Uid) ||
            (user.Email != null && c.Email == user.Email), ct);

        if (exists) return Conflict(new { message = "Já existe um perfil de Client para este utilizador." });

        var c = new Domain.Clients.Client(
            firstName: body.FirstName.Trim(),
            lastName:  body.LastName.Trim(),
            email:     (user.Email ?? body.Email).Trim().ToLowerInvariant(),
            citizenCardNumber: body.CitizenCardNumber.Trim(),
            phone:     body.Phone?.Trim(),
            birthDate: body.BirthDate
        );

        // ligar ao UID
        typeof(Domain.Clients.Client).GetProperty("FirebaseUid")!.SetValue(c, user.Uid);

        db.Clients.Add(c);
        await db.SaveChangesAsync(ct);

        return Created($"/api/v1/clients/{c.Id}", new { c.Id });
    }

    // ainda em MeOnboardingController
    [HttpPost("claim-staff")]
    [Authorize(Policy = "EmailVerified")]
    public async Task<IActionResult> ClaimStaff([FromBody] ClaimStaffBody body, CancellationToken ct)
    {
        // Encontrar staff por staffNumber (e opcionalmente email)
        var staff = await db.Staff.FirstOrDefaultAsync(s =>
            s.StaffNumber == body.StaffNumber &&
            (body.Email == null || s.Email == body.Email), ct);

        if (staff is null)
            return NotFound(new { message = "Staff não encontrado." });

        if (!string.IsNullOrWhiteSpace(staff.FirebaseUid))
            return Conflict(new { message = "Este Staff já está associado a outra conta." });

        // Ligar UID
        typeof(Domain.Staff.Staff).GetProperty("FirebaseUid")!.SetValue(staff, user.Uid);

        // Atribuir role Staff (se ainda não existir)
        var hasRole = await db.RoleAssignments.AnyAsync(r =>
            r.FirebaseUid == user.Uid && r.Role == Domain.Auth.RoleName.Staff, ct);

        if (!hasRole)
            db.RoleAssignments.Add(new Domain.Auth.RoleAssignment(user.Uid!, Domain.Auth.RoleName.Staff));

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Conta associada como Staff.", staffId = staff.Id });
    }

public record ClaimStaffBody(string StaffNumber, string? Email);


    public record CreateClientSelf(
        string FirstName,
        string LastName,
        string CitizenCardNumber,
        string Email,
        string? Phone,
        DateOnly? BirthDate);
}
