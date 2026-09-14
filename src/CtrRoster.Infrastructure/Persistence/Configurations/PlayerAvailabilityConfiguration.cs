using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtrRoster.Infrastructure.Persistence.Configurations;

public class PlayerAvailabilityConfiguration : IEntityTypeConfiguration<PlayerAvailability>
{
    public void Configure(EntityTypeBuilder<PlayerAvailability> builder)
    {
        builder.ToTable("PlayerAvailabilities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.DiscordUserId)
            .IsRequired();

        builder.Property(a => a.DiscordUsername)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.IsAbsent)
            .IsRequired();

        builder.Property(a => a.PreferredGamesJson)
            .IsRequired();

        // Un joueur n'a qu'un seul enregistrement de disponibilité par session
        builder.HasIndex(a => new { a.GameSessionId, a.DiscordUserId })
            .IsUnique();
    }
}
