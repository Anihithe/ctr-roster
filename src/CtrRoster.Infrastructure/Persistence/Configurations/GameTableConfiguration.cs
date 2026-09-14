using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtrRoster.Infrastructure.Persistence.Configurations;

public class GameTableConfiguration : IEntityTypeConfiguration<GameTable>
{
    public void Configure(EntityTypeBuilder<GameTable> builder)
    {
        builder.ToTable("GameTables");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.GameName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.CreatedByDiscordUserId)
            .IsRequired();

        builder.HasIndex(t => t.GameSessionId);

        builder.HasMany(t => t.Participants)
            .WithOne(p => p.GameTable)
            .HasForeignKey(p => p.GameTableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
