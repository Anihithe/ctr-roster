using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtrRoster.Infrastructure.Persistence.Configurations;

public class TableParticipantConfiguration : IEntityTypeConfiguration<TableParticipant>
{
    public void Configure(EntityTypeBuilder<TableParticipant> builder)
    {
        builder.ToTable("TableParticipants");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.DiscordUserId)
            .IsRequired();

        builder.Property(p => p.DiscordUsername)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Role)
            .IsRequired()
            .HasConversion<int>();

        // Un utilisateur ne peut s'inscrire qu'une seule fois dans la même table
        builder.HasIndex(p => new { p.GameTableId, p.DiscordUserId })
            .IsUnique();
    }
}
