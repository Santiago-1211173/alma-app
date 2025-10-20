using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.GroupClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations
{
    public sealed class GroupClassConfiguration : IEntityTypeConfiguration<GroupClass>
    {
        public void Configure(EntityTypeBuilder<GroupClass> b)
        {
            b.ToTable("GroupClasses");
            b.HasKey(x => x.Id);

            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.DurationMinutes).IsRequired();
            b.Property(x => x.MaxParticipants).IsRequired();
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Category).HasConversion<int>().IsRequired();
            b.Property(x => x.StartLocal).IsRequired();
            b.Property(x => x.CreatedAtLocal).IsRequired();
            b.Property(x => x.RowVersion).IsRowVersion();

            b.HasIndex(x => new { x.InstructorId, x.StartLocal });
            b.HasIndex(x => new { x.RoomId, x.StartLocal });

            b.HasMany(x => x.Participants)
             .WithOne()
             .HasForeignKey(p => p.GroupClassId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
