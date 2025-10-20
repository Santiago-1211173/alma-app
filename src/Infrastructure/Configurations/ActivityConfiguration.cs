using AlmaApp.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations
{
    public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
    {
        public void Configure(EntityTypeBuilder<Activity> b)
        {
            b.ToTable("Activities");
            b.HasKey(a => a.Id);

            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.Description).HasMaxLength(2000);
            b.Property(a => a.Category).HasConversion<int>().IsRequired();
            b.Property(a => a.RoomId).IsRequired();
            b.Property(a => a.InstructorId).IsRequired();
            b.Property(a => a.StartLocal).IsRequired();
            b.Property(a => a.DurationMinutes).IsRequired();
            b.Property(a => a.MaxParticipants).IsRequired();
            b.Property(a => a.Status).HasConversion<int>().IsRequired();
            b.Property(a => a.CreatedAtLocal).IsRequired();
            b.Property(a => a.RowVersion).IsRowVersion();

            b.HasIndex(a => new { a.RoomId, a.StartLocal });
            b.HasIndex(a => new { a.InstructorId, a.StartLocal });

            b.HasMany(a => a.Participants)
             .WithOne()
             .HasForeignKey(p => p.ActivityId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
