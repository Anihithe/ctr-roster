using CtrRoster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtrRoster.Infrastructure.Persistence.Configurations;

public class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.ToTable("GameSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScheduledDate)
            .IsRequired();

        builder.Property(s => s.DiscordChannelId)
            .IsRequired();

        builder.Property(s => s.DiscordMessageId)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.ScheduledDate);

        builder.HasMany(s => s.Tables)
            .WithOne(t => t.GameSession)
            .HasForeignKey(t => t.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Availabilities)
            .WithOne(a => a.GameSession)
            .HasForeignKey(a => a.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
