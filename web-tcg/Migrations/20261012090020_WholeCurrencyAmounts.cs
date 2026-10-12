using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class WholeCurrencyAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert legacy fractional amounts before enforcing whole currency values.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" SET "Balance" = trunc("Balance"), "CardBalance" = trunc("CardBalance");
                UPDATE "CardSets" SET "BoosterPrice" = trunc("BoosterPrice");
                UPDATE "MarketAuctions" SET "StartingPrice" = trunc("StartingPrice"), "CurrentPrice" = trunc("CurrentPrice");
                UPDATE "MarketBids" SET "Amount" = trunc("Amount");
                UPDATE "FriendTrades" SET "OfferedMoney" = trunc("OfferedMoney"), "RequestedMoney" = trunc("RequestedMoney");
                """);
            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketBid_WholeAmount",
                table: "MarketBids",
                sql: "\"Amount\" = trunc(\"Amount\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MarketAuction_WholePrices",
                table: "MarketAuctions",
                sql: "\"StartingPrice\" = trunc(\"StartingPrice\") AND \"CurrentPrice\" = trunc(\"CurrentPrice\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FriendTrade_WholeMoney",
                table: "FriendTrades",
                sql: "\"OfferedMoney\" = trunc(\"OfferedMoney\") AND \"RequestedMoney\" = trunc(\"RequestedMoney\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CardSet_WholePrice",
                table: "CardSets",
                sql: "\"BoosterPrice\" = trunc(\"BoosterPrice\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_WholeBalances",
                table: "AspNetUsers",
                sql: "\"Balance\" = trunc(\"Balance\") AND \"CardBalance\" = trunc(\"CardBalance\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketBid_WholeAmount",
                table: "MarketBids");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MarketAuction_WholePrices",
                table: "MarketAuctions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FriendTrade_WholeMoney",
                table: "FriendTrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CardSet_WholePrice",
                table: "CardSets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_WholeBalances",
                table: "AspNetUsers");
        }
    }
}
