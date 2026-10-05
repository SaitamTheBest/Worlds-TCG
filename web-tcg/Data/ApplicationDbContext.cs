using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data.Entities;

namespace web_tcg.Data
{
    public class ApplicationDbContext: IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<CardSet> CardSets { get; set; }
        public DbSet<MarketAuction> MarketAuctions { get; set; }
        public DbSet<MarketBid> MarketBids { get; set; }
        public DbSet<Card> Cards { get; set; }

        public DbSet<Region> Regions { get; set; }

        public DbSet<Country> Countries { get; set; }
        public DbSet<PoliticalParty> PoliticalParties { get; set; }

        public DbSet<CardAbility> CardAbilities { get; set; }

        public DbSet<LeaderCard> LeaderCards { get; set; }

        public DbSet<CharacterCard> CharacterCards { get; set; }

        public DbSet<PoliticalDecisionCard> PoliticalDecisionCards { get; set; }

        public DbSet<LocationCard> LocationCards { get; set; }

        public DbSet<UserCardCollection> UserCardCollections { get; set; }

        public DbSet<BoosterOpening> BoosterOpenings { get; set; }

        public DbSet<BoosterOpeningCard> BoosterOpeningCards { get; set; }

        public DbSet<Notification> Notifications { get; set; }

        public DbSet<UserProfileShowcaseCard> UserProfileShowcaseCards { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PoliticalParty>().HasOne(p => p.Country).WithMany()
                .HasForeignKey(p => p.CountryId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PoliticalParty>().HasIndex(p => new { p.CountryId, p.Name }).IsUnique();
            modelBuilder.Entity<Card>().HasOne(c => c.PoliticalParty).WithMany()
                .HasForeignKey(c => c.PoliticalPartyId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarketAuction>(entity =>
            {
                entity.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.HighestBidder).WithMany().HasForeignKey(x => x.HighestBidderId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(x => x.StartingPrice).HasPrecision(18, 2);
                entity.Property(x => x.CurrentPrice).HasPrecision(18, 2);
                entity.HasIndex(x => new { x.Status, x.EndsAt });
                entity.HasIndex(x => new { x.SellerId, x.Status });
                entity.ToTable(t => t.HasCheckConstraint("CK_MarketAuction_Prices", "\"StartingPrice\" >= 1 AND \"CurrentPrice\" >= \"StartingPrice\""));
            });
            modelBuilder.Entity<MarketBid>(entity =>
            {
                entity.HasOne(x => x.Auction).WithMany(x => x.Bids).HasForeignKey(x => x.AuctionId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Bidder).WithMany().HasForeignKey(x => x.BidderId).OnDelete(DeleteBehavior.Restrict);
                entity.Property(x => x.Amount).HasPrecision(18, 2);
                entity.HasIndex(x => new { x.AuctionId, x.CreatedAt });
            });

            modelBuilder.Entity<Card>()
                .HasIndex(x => new
                {
                    x.CardSetId,
                    x.RegionId,
                    x.CollectionNumber
                });

            modelBuilder.Entity<Card>()
                .HasOne(c => c.BaseCard)
                .WithMany(c => c.Variants)
                .HasForeignKey(c => c.BaseCardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Card>().HasIndex(c => c.CardSetId)
                .IsUnique().HasFilter("\"CollectionNumber\" = 0")
                .HasDatabaseName("IX_Cards_SecretPerSet");
            modelBuilder.Entity<Card>().ToTable(table => table.HasCheckConstraint("CK_Cards_Secret",
                "(\"Type\" = 5 AND \"CollectionNumber\" = 0 AND \"Rarity\" = 5 AND \"Rarity\" IS NOT NULL AND \"UsageCost\" = 0 AND \"BaseCardId\" IS NULL AND \"VariantNumber\" IS NULL) OR (\"Type\" <> 5 AND \"CollectionNumber\" > 0 AND (\"Rarity\" IS NULL OR \"Rarity\" <> 5))"));

            modelBuilder.Entity<Card>()
                .HasIndex(c => new
                {
                    c.BaseCardId,
                    c.VariantNumber
                })
                .IsUnique();

            modelBuilder.Entity<Card>()
                .HasIndex(x => new
                {
                    x.CardSetId,
                    x.RegionId,
                    x.CollectionNumber
                })
                .IsUnique()
                .HasFilter("\"BaseCardId\" IS NULL");

            modelBuilder.Entity<Region>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<Country>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<CardSet>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<CardSet>()
                .Property(x => x.ReleaseDate)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<LeaderCard>()
                .HasIndex(x => x.CardId)
                .IsUnique();

            modelBuilder.Entity<CharacterCard>()
                .HasIndex(x => x.CardId)
                .IsUnique();

            modelBuilder.Entity<PoliticalDecisionCard>()
                .HasIndex(x => x.CardId)
                .IsUnique();

            modelBuilder.Entity<LocationCard>()
                .HasIndex(x => x.CardId)
                .IsUnique();

            modelBuilder.Entity<UserCardCollection>()
                .HasIndex(x => new { x.UserId, x.CardId })
                .IsUnique();

            modelBuilder.Entity<UserCardCollection>()
                .HasIndex(x => x.UserId);

            modelBuilder.Entity<UserCardCollection>()
                .HasIndex(x => x.AcquiredDate);

            modelBuilder.Entity<UserCardCollection>()
                .Property(x => x.AcquiredDate)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<BoosterOpening>()
                .HasMany(x => x.Cards)
                .WithOne(x => x.BoosterOpening)
                .HasForeignKey(x => x.BoosterOpeningId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BoosterOpeningCard>()
                .HasOne(x => x.Card)
                .WithMany()
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserCardCollection>()
                .HasOne(x => x.Card)
                .WithMany()
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserCardCollection>()
                .HasIndex(x => new
                {
                    x.UserId,
                    x.CardId
                })
                .IsUnique();

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.IsRead
                });

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.CreatedAt
                });
            });

            modelBuilder.Entity<UserProfileShowcaseCard>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.User)
                    .WithMany(x => x.ShowcaseCards)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Card)
                    .WithMany()
                    .HasForeignKey(x => x.CardId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.CardId
                })
                .IsUnique();

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.Position
                })
                .IsUnique();
            });

            modelBuilder.ConfigureGameModel();
        }
    }
}
