using Microsoft.EntityFrameworkCore;
using web_tcg.Data.Entities;

namespace web_tcg.Data
{
    public static class GameModelBuilderExtensions
    {
        public static void ConfigureGameModel(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Deck>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Deck>()
                .HasOne(x => x.LeaderCard)
                .WithMany()
                .HasForeignKey(x => x.LeaderCardId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DeckCard>()
                .HasOne(x => x.Deck)
                .WithMany(x => x.Cards)
                .HasForeignKey(x => x.DeckId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeckCard>()
                .HasOne(x => x.Card)
                .WithMany()
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GameMatch>()
                .HasMany(x => x.Logs)
                .WithOne(x => x.GameMatch)
                .HasForeignKey(x => x.GameMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameMatch>()
                .HasMany(x => x.ChatMessages)
                .WithOne(x => x.GameMatch)
                .HasForeignKey(x => x.GameMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CardAbilityEffect>()
                .HasOne(x => x.CardAbility)
                .WithMany(x => x.Effects)
                .HasForeignKey(x => x.CardAbilityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CardAbilityCondition>()
                .HasOne(x => x.CardAbility)
                .WithMany(x => x.Conditions)
                .HasForeignKey(x => x.CardAbilityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GameMatch>()
                .HasIndex(x => x.Status);

            modelBuilder.Entity<GameLogEntry>()
                .HasIndex(x => new
                {
                    x.GameMatchId,
                    x.CreatedAt
                });

            modelBuilder.Entity<GameChatMessage>()
                .HasIndex(x => new
                {
                    x.GameMatchId,
                    x.CreatedAt
                });
        }
    }
}