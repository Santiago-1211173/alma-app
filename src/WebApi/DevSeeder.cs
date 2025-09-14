using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Clients;
using AlmaApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi;

internal static class DevSeeder
{
    public static async Task SeedAsync(AppDbContext db)
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

        await db.AddRangeAsync(list);
        await db.SaveChangesAsync();
    }
}