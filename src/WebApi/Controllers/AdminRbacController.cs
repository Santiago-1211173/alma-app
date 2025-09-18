using AlmaApp.Domain.Auth;
using AlmaApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/v1/rbac")]
public class AdminRbacController(AppDbContext db) : ControllerBase
{
    // GET /api/v1/rbac/users/{uid}/roles
    [HttpGet("users/{uid}/roles")]
    public async Task<IActionResult> GetRoles(string uid)
    {
        var roles = await db.RoleAssignments
            .Where(r => r.FirebaseUid == uid) // <-- AJUSTA se a tua prop tiver outro nome
            .Select(r => r.Role)
            .ToListAsync();

        return Ok(new { userUid = uid, roles });
    }

    public record AssignRoleRequest(RoleName Role);

    // POST /api/v1/rbac/users/{uid}/roles
    [HttpPost("users/{uid}/roles")]
    public async Task<IActionResult> AssignRole(string uid, [FromBody] AssignRoleRequest body)
    {
        var exists = await db.RoleAssignments
            .AnyAsync(r => r.FirebaseUid == uid && r.Role == body.Role); // <-- AJUSTA nome da prop

        if (exists)
            return Conflict(new { message = "Role já atribuída a este utilizador." });

        // usa a factory/ctor público
        var entity = new RoleAssignment(uid, body.Role);
        db.RoleAssignments.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRoles), new { uid }, null);
    }

    // DELETE /api/v1/rbac/users/{uid}/roles/{role}
    [HttpDelete("users/{uid}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(string uid, RoleName role)
    {
        var entity = await db.RoleAssignments
            .FirstOrDefaultAsync(r => r.FirebaseUid == uid && r.Role == role); // <-- AJUSTA nome da prop

        if (entity is null) return NotFound();

        db.RoleAssignments.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
