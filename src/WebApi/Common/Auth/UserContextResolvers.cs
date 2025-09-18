using AlmaApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Common.Auth;

public static class UserContextResolvers
{
    public static async Task<Guid> RequireClientIdAsync(
        this IUserContext user, AppDbContext db, CancellationToken ct = default)
    {
        var uid = user.Uid;
        var email = user.Email;

        var id = await db.Clients.AsNoTracking()
            .Where(c =>
                (uid   != null && c.FirebaseUid == uid) ||
                (email != null && c.Email == email))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (id is null)
            throw new InvalidOperationException("O utilizador autenticado não tem perfil de Client.");

        return id.Value;
    }

    public static async Task<Guid> RequireStaffIdAsync(
        this IUserContext user, AppDbContext db, CancellationToken ct = default)
    {
        var uid = user.Uid;
        var email = user.Email;

        var id = await db.Staff.AsNoTracking()
            .Where(s =>
                (uid   != null && s.FirebaseUid == uid) ||
                (email != null && s.Email == email))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (id is null)
            throw new InvalidOperationException("O utilizador autenticado não tem perfil de Staff.");

        return id.Value;
    }
}
