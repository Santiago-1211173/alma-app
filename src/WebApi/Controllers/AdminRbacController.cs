using System;
using System.Linq;
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
        var (ok, problem) = ValidateUid(uid);
        if (!ok) return BadRequest(problem);

        var roles = await db.RoleAssignments
            .Where(r => r.FirebaseUid == uid.Trim())
            .Select(r => r.Role)
            .ToListAsync();

        return Ok(new { userUid = uid.Trim(), roles });
    }

    public sealed record AssignRoleRequest(RoleName Role);

    // POST /api/v1/rbac/users/{uid}/roles
    [HttpPost("users/{uid}/roles")]
    public async Task<IActionResult> AssignRole(string uid, [FromBody] AssignRoleRequest body)
    {
        var (ok, problem) = ValidateUid(uid);
        if (!ok) return BadRequest(problem);

        uid = uid.Trim();

        var exists = await db.RoleAssignments
            .AnyAsync(r => r.FirebaseUid == uid && r.Role == body.Role);

        if (exists)
            return Conflict(new
            {
                title = "Conflict",
                detail = "Role já atribuída a este utilizador."
            });

        // cria a atribuição
        var entity = new RoleAssignment(uid, body.Role);
        db.RoleAssignments.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRoles), new { uid }, null);
    }

    // DELETE /api/v1/rbac/users/{uid}/roles/{role}
    [HttpDelete("users/{uid}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(string uid, RoleName role)
    {
        var (ok, problem) = ValidateUid(uid);
        if (!ok) return BadRequest(problem);

        uid = uid.Trim();

        var entity = await db.RoleAssignments
            .FirstOrDefaultAsync(r => r.FirebaseUid == uid && r.Role == role);

        if (entity is null) return NotFound();

        db.RoleAssignments.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Valida se a string recebida parece um Firebase UID (e não um JWT).
    /// - obrigatório, sem espaços nos extremos;
    /// - comprimento ≤ 128 (limite do Firebase/BD);
    /// - não pode conter '.' (padrão característico de JWT: header.payload.signature);
    /// - se contiver dois pontos ('.') ou começar por "eyJ" e tiver pontos → muito provavelmente é um JWT.
    /// </summary>
    private static (bool ok, object problem) ValidateUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return (false, new
            {
                type = "https://httpstatuses.com/400",
                title = "Dados inválidos",
                detail = "Parâmetro 'uid' é obrigatório.",
                status = 400
            });
        }

        var trimmed = uid.Trim();

        if (trimmed.Length > 128)
        {
            return (false, new
            {
                type = "https://httpstatuses.com/400",
                title = "Dados inválidos",
                detail = "O 'uid' excede o tamanho máximo (128). Verifica se não estás a enviar o token JWT inteiro. Usa o UID devolvido pelo endpoint /whoami.",
                status = 400
            });
        }

        // Sinais fortes de JWT
        var dotCount = trimmed.Count(c => c == '.');
        var looksLikeJwt = dotCount >= 2 || (trimmed.StartsWith("eyJ", StringComparison.Ordinal) && dotCount >= 1);

        if (looksLikeJwt)
        {
            return (false, new
            {
                type = "https://httpstatuses.com/400",
                title = "Dados inválidos",
                detail = "Foi detetado um token JWT no lugar do Firebase UID. Usa o 'uid' devolvido por /whoami (não o token).",
                status = 400
            });
        }

        return (true, new { });
    }
}
