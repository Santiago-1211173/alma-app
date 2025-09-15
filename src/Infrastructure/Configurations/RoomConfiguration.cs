using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("Rooms");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Capacity).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.Name).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_Room_Capacity_Positive", "[Capacity] > 0"));    }
}
