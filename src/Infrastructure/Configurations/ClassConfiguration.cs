using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> b)
    {
        b.ToTable("Classes");
        b.HasKey(x => x.Id);

        b.Property(x => x.ClientId).IsRequired();
        b.Property(x => x.StaffId).IsRequired();
        b.Property(x => x.RoomId).IsRequired();

        b.Property(x => x.StartUtc).IsRequired();
        b.Property(x => x.DurationMinutes).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();

        b.Property(x => x.CreatedByUid).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()").IsRequired();

        b.Property(x => x.RowVersion).IsRowVersion();

        // índices para pesquisa e conflitos
        b.HasIndex(x => new { x.StaffId, x.StartUtc });
        b.HasIndex(x => new { x.RoomId,  x.StartUtc });
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ClientId);
    }
}
