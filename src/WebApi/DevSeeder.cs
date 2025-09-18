using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Clients;
using AlmaApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using AlmaApp.Domain.Staff;
using AlmaApp.Domain.Rooms;
using AlmaApp.Domain.Auth;

namespace AlmaApp.WebApi;

internal static class DevSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration cfg)
    {
        // Só semeia se estiver vazio
        if (await db.Clients.AnyAsync()) return;

        var list = new List<Client>();
        // cria 25 clientes fictícios (valores únicos p/ não chocar com índices)
        for (int i = 1; i <= 25; i++)
        {
            var first = $"Ana{i:00}";
            var last  = $"Silva{i:00}";
            var cc    = $"{10000000 + i}";
            var email = $"ana{i:00}@example.com";
            var phone = $"+35191{i:0000000}";
            var birth = new DateOnly(1990 + (i % 10), (i % 12) + 1, ((i % 27) + 1));
            list.Add(new Client(first, last, cc, email, phone, birth));
        }

        if (!await db.Staff.AnyAsync())
        {
            db.Staff.AddRange(
                new Staff("João", "Pereira", "joao.staff@example.com", "+351910000001", "STF-001", "Fisioterapia"),
                new Staff("Marta", "Costa", "marta.staff@example.com", "+351910000002", "STF-002", "Pilates")
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Rooms.AnyAsync())
        {
            db.Rooms.AddRange(
                new Room("Sala A", 12, true),
                new Room("Sala B", 8, true)
            );
        }

        var uid = cfg["Seed:AdminFirebaseUid"];
        if (!string.IsNullOrWhiteSpace(uid))
        {
            var hasAdmin = await db.RoleAssignments
                .AsNoTracking()
                .AnyAsync(x => x.FirebaseUid == uid && x.Role == RoleName.Admin);

            if (!hasAdmin)
            {
                db.RoleAssignments.Add(new RoleAssignment(uid, RoleName.Admin));
                await db.SaveChangesAsync();
            }
        }

        await db.AddRangeAsync(list);
        await db.SaveChangesAsync();
    }
}