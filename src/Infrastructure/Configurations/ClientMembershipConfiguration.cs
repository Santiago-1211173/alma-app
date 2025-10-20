using AlmaApp.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmaApp.Infrastructure.Configurations
{
    /// <summary>
    /// Configures the ClientMembership entity for EF Core. Ensures that only one
    /// active membership exists per client at any given time by creating a
    /// partial unique index on (ClientId, Status) where Status == Active (0).
    /// </summary>
    public sealed class ClientMembershipConfiguration : IEntityTypeConfiguration<ClientMembership>
    {
        public void Configure(EntityTypeBuilder<ClientMembership> builder)
        {
            builder.ToTable("ClientMemberships");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClientId).IsRequired();
            builder.Property(x => x.StartUtc).IsRequired();
            builder.Property(x => x.Status).IsRequired();
            builder.Property(x => x.BillingPeriod).IsRequired();
            builder.Property(x => x.Nif).HasMaxLength(20);
            builder.Property(x => x.CreatedByUid).HasMaxLength(128).IsRequired();
            builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()").IsRequired();
            builder.Property(x => x.RowVersion).IsRowVersion();

            // Only one active membership per client. SQL Server uses numeric values for enums.
            builder.HasIndex(x => new { x.ClientId, x.Status })
                   .HasFilter("[Status] = 0")
                   .IsUnique();

            // Index to quickly query memberships by client and start date
            builder.HasIndex(x => new { x.ClientId, x.StartUtc });
        }
    }
}