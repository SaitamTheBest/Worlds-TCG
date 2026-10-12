using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Models.Balance;

public static class CurrencyChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TCG_MARKET_TEST_CONNECTION") ?? throw new Exception("Base temporaire requise.");
        var database = "currency_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}",admin)) await create.ExecuteNonQueryAsync();
        var checks = 0;
        void Check(bool valid,string label) { if(!valid) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connection) { Database = database };
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261009084143_AddFriendsMessagesAndTrades");
            var a = new ApplicationUser { Id="currency-a", UserName="currency-a", Balance=100.75m,CardBalance=50.25m };
            var b = new ApplicationUser { Id="currency-b", UserName="currency-b" };
            var set = new CardSet { Code="INT",Name="Test entiers",ReleaseDate=DateTime.UtcNow,BoosterPrice=100.9m,BoosterImageUrl="test" };
            var card = new Card { Name="Entier",CardSet=set,Region=new Region { Name="Test",Code="INT" },CollectionNumber=1,Type=web_tcg.Data.Enums.CardType.Character,ImageUrl="test" };
            var friendship = new Friendship { FirstUserId=a.Id,SecondUserId=b.Id,RequesterId=a.Id,Status=FriendshipStatus.Accepted };
            var auction = new MarketAuction { SellerId=a.Id,Card=card,HighestBidderId=b.Id,StartingPrice=10.5m,CurrentPrice=20.75m,EndsAt=DateTime.UtcNow.AddHours(1) };
            var bid = new MarketBid { Auction=auction,BidderId=b.Id,Amount=20.75m };
            var trade = new FriendTrade { Friendship=friendship,ProposerId=a.Id,RecipientId=b.Id,OfferedMoney=10.5m,RequestedMoney=2.75m };
            db.AddRange(a,b,set,card,friendship,auction,bid,trade);
            await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            a = await db.Users.SingleAsync(x=>x.Id==a.Id);
            Check(a.Balance==100 && a.CardBalance==50,"monnaies : migration des deux soldes à l'entier inférieur");
            Check(await db.CardSets.AnyAsync(x=>x.Id==set.Id && x.BoosterPrice==100),"monnaies : migration du prix des boosters");
            Check(await db.MarketAuctions.AnyAsync(x=>x.Id==auction.Id && x.StartingPrice==10 && x.CurrentPrice==20)
                && await db.MarketBids.AnyAsync(x=>x.Id==bid.Id && x.Amount==20),"monnaies : migration des prix et mises sans modifier la carte réservée");
            Check(await db.FriendTrades.AnyAsync(x=>x.Id==trade.Id && x.OfferedMoney==10 && x.RequestedMoney==2),"monnaies : migration des propositions d'échange");
            foreach (var (table,column) in new[] { ("AspNetUsers","Balance"),("AspNetUsers","CardBalance"),("CardSets","BoosterPrice"),
                ("MarketAuctions","StartingPrice"),("MarketAuctions","CurrentPrice"),("MarketBids","Amount"),("FriendTrades","OfferedMoney"),("FriendTrades","RequestedMoney") })
            {
                var refused=false;
                var statement = $"UPDATE \"{table}\" SET \"{column}\" = \"{column}\" + 0.5";
                try { await db.Database.ExecuteSqlRawAsync(statement); }
                catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.CheckViolation) { refused=true; }
                Check(refused,$"monnaies : la base refuse les décimales dans {table}.{column}");
            }
            Check(new WholeCurrencyAttribute().IsValid(1500m) && !new WholeCurrencyAttribute().IsValid(1.5m),"monnaies : validation des formulaires entiers");
            Console.WriteLine($"{checks} vérifications monnaies réussies.");
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
