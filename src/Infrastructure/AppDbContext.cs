using AlmaApp.Domain.Activities;
using AlmaApp.Domain.ClassRequests;
using AlmaApp.Domain.Classes;
using AlmaApp.Domain.Clients;
using AlmaApp.Domain.Rooms;
using AlmaApp.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.Infrastructure;

/// <summary>
/// Representa o contexto de base de dados da aplicação. Inclui DbSets para
/// todas as entidades de domínio e configurações extra para índices e
/// unicidades. Esta versão foi actualizada para incluir a entidade Activity.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> opts) : DbContext(opts)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Room>  Rooms => Set<Room>();
    public DbSet<ClassRequest> ClassRequests => Set<ClassRequest>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Domain.Auth.RoleAssignment> RoleAssignments => Set<Domain.Auth.RoleAssignment>();
    public DbSet<Domain.Availability.StaffAvailabilityRule> StaffAvailabilityRules => Set<Domain.Availability.StaffAvailabilityRule>();
    public DbSet<Domain.Availability.StaffTimeOff> StaffTimeOffs => Set<Domain.Availability.StaffTimeOff>();
    public DbSet<Domain.Availability.RoomClosure> RoomClosures => Set<Domain.Availability.RoomClosure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas as IEntityTypeConfiguration<> deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        // Índices de Firebase Uid para Client e Staff
        modelBuilder.Entity<Client>()
            .HasIndex(c => c.FirebaseUid)
            .IsUnique()
            .HasFilter("[FirebaseUid] IS NOT NULL");

        modelBuilder.Entity<Staff>()
            .HasIndex(s => s.FirebaseUid)
            .IsUnique()
            .HasFilter("[FirebaseUid] IS NOT NULL");
    }
}