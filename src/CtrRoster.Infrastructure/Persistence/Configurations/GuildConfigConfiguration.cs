using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtrRoster.Infrastructure.Persistence.Configurations;

public class GuildConfigConfiguration : IEntityTypeConfiguration<GuildConfig>
{
    public void Configure(EntityTypeBuilder<GuildConfig> builder)
    {
        builder.ToTable("GuildConfigs");

        builder.HasKey(c => c.GuildId);
        builder.Property(c => c.GuildId).ValueGeneratedNever();

        builder.Property(c => c.AdminRoleId)
            .IsRequired(false);

        builder.Property(c => c.DefaultChannelId)
            .IsRequired(false);

        builder.Property(c => c.AllowedChannelId)
            .IsRequired(false);

        builder.Property(c => c.AutoRenewSessions)
            .IsRequired();

        builder.Property(c => c.RenewIntervalDays)
            .IsRequired();

        builder.Property(c => c.RenewIntervalHours)
            .IsRequired(false);

        builder.Property(c => c.DefaultMaxTables)
            .IsRequired(false);

        builder.Property(c => c.OpenDaysJson)
            .IsRequired(false);
    }
}
