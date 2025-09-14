using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using AlmaApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AlmaApp.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Tenta carregar appsettings tanto do projeto atual como do WebApi
        var basePath = Directory.GetCurrentDirectory();

        var cfg = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.Development.json", optional: true)                  // caso corras a partir do WebApi
            .AddJsonFile(Path.Combine("..", "WebApi", "appsettings.Development.json"),   // caso corras a partir do Infrastructure
                         optional: true)
            .AddEnvironmentVariables() // permite usar ConnectionStrings__DefaultConnection no ambiente
            .Build();

        var cs =
            cfg.GetConnectionString("DefaultConnection")
            ?? System.Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost,1433;Database=alma_app_dev;User Id=sa;Password=Str0ng!Passw0rd;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AppDbContext(options);
    }
}