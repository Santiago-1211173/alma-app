using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.ClassRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations;

public sealed class ClassRequestConfiguration : IEntityTypeConfiguration<ClassRequest>
{
    public void Configure(EntityTypeBuilder<ClassRequest> b)
    {
        b.ToTable("ClassRequests");
        b.HasKey(x => x.Id);

        b.Property(x => x.ClientId).IsRequired();
        b.Property(x => x.StaffId).IsRequired();
        b.Property(x => x.ProposedStartUtc).IsRequired();
        b.Property(x => x.DurationMinutes).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedByUid).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.Property(x => x.Notes).HasMaxLength(500);

        // Índices úteis para pesquisa e conflitos
        b.HasIndex(x => new { x.StaffId, x.ProposedStartUtc });
        b.HasIndex(x => new { x.ClientId, x.ProposedStartUtc });
        b.HasIndex(x => x.Status);
    }
}
