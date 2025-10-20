using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using AlmaApp.Domain.ServiceAppointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class ServiceAppointmentConfiguration : IEntityTypeConfiguration<ServiceAppointment>
{
    public void Configure(EntityTypeBuilder<ServiceAppointment> b)
    {
        b.ToTable("ServiceAppointments");
        b.HasKey(x => x.Id);

        b.Property(x => x.ClientId).IsRequired();
        b.Property(x => x.SecondClientId);
        b.Property(x => x.StaffId).IsRequired();
        b.Property(x => x.RoomId).IsRequired();
        b.Property(x => x.ServiceType).HasConversion<int>().IsRequired();
        b.Property(x => x.StartUtc).IsRequired();
        b.Property(x => x.DurationMinutes).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedByUid).HasMaxLength(128).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        // Índices para acelerar verificação de conflitos de agenda
        b.HasIndex(x => new { x.StaffId, x.StartUtc });
        b.HasIndex(x => new { x.RoomId, x.StartUtc });
    }
}