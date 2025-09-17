using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlmaApp.Domain.Clients;
using AlmaApp.Domain.Staff;
using AlmaApp.Domain.Rooms;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;

namespace AlmaApp.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> opts) : DbContext(opts)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Room>  Rooms => Set<Room>();
    public DbSet<ClassRequest> ClassRequests => Set<ClassRequest>();
    public DbSet<Class> Classes => Set<Class>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas as IEntityTypeConfiguration<> deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}