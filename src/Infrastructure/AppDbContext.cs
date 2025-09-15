using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlmaApp.Domain.Clients;
using AlmaApp.Domain.Staff;
using AlmaApp.Domain.Rooms;


namespace AlmaApp.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> opts) : DbContext(opts)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Room>  Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas as IEntityTypeConfiguration<> deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}