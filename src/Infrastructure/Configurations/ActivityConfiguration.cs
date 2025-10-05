using AlmaApp.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

/// <summary>
/// Configuração EF Core para a entidade Activity. Define tabela, chaves,
/// comprimentos de campos e token de concorrência optimista.
/// </summary>
public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> b)
    {
        b.ToTable("Activities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.Description)
            .HasMaxLength(500);

        b.Property(x => x.RoomId)
            .IsRequired();

        b.Property(x => x.StartUtc)
            .IsRequired();

        b.Property(x => x.DurationMinutes)
            .IsRequired();

        b.Property(x => x.Status)
            .IsRequired();

        b.Property(x => x.CreatedByUid)
            .HasMaxLength(128)
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        b.Property(x => x.RowVersion)
            .IsRowVersion();

        // Indice para ordenar rapidamente por data de início e sala
        b.HasIndex(x => new { x.RoomId, x.StartUtc });
    }
}