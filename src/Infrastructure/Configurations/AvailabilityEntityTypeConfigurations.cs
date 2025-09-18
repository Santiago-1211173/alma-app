using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class StaffAvailabilityRuleConfig : IEntityTypeConfiguration<StaffAvailabilityRule>
{
    public void Configure(EntityTypeBuilder<StaffAvailabilityRule> b)
    {
        b.ToTable("StaffAvailabilityRules");
        b.HasKey(x => x.Id);

        b.Property(x => x.StartTimeUtc).IsRequired();
        b.Property(x => x.EndTimeUtc).IsRequired();
        b.Property(x => x.Active).HasDefaultValue(true);

        // Índice útil para pesquisa por staff/dia
        b.HasIndex(x => new { x.StaffId, x.DayOfWeek });
    }
}

public sealed class StaffTimeOffConfig : IEntityTypeConfiguration<StaffTimeOff>
{
    public void Configure(EntityTypeBuilder<StaffTimeOff> b)
    {
        b.ToTable("StaffTimeOffs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(400);

        // Índice para janelas temporais por staff
        b.HasIndex(x => new { x.StaffId, x.FromUtc });
    }
}

public sealed class RoomClosureConfig : IEntityTypeConfiguration<RoomClosure>
{
    public void Configure(EntityTypeBuilder<RoomClosure> b)
    {
        b.ToTable("RoomClosures");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(400);

        // Índice para janelas temporais por sala
        b.HasIndex(x => new { x.RoomId, x.FromUtc });
    }
}
