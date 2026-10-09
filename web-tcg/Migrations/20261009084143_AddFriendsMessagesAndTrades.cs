using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendsMessagesAndTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstUserId = table.Column<string>(type: "text", nullable: false),
                    SecondUserId = table.Column<string>(type: "text", nullable: false),
                    RequesterId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                    table.CheckConstraint("CK_Friendship_Pair", "\"FirstUserId\" <> \"SecondUserId\" AND (\"RequesterId\" = \"FirstUserId\" OR \"RequesterId\" = \"SecondUserId\")");
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_FirstUserId",
                        column: x => x.FirstUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Friendships_AspNetUsers_SecondUserId",
                        column: x => x.SecondUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FriendMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendMessages_Friendships_FriendshipId",
                        column: x => x.FriendshipId,
                        principalTable: "Friendships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FriendTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FriendshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposerId = table.Column<string>(type: "text", nullable: false),
                    RecipientId = table.Column<string>(type: "text", nullable: false),
                    OfferedMoney = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedMoney = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendTrades", x => x.Id);
                    table.CheckConstraint("CK_FriendTrade_Money", "\"OfferedMoney\" >= 0 AND \"RequestedMoney\" >= 0");
                    table.ForeignKey(
                        name: "FK_FriendTrades_Friendships_FriendshipId",
                        column: x => x.FriendshipId,
                        principalTable: "Friendships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FriendTradeCards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    IsOffered = table.Column<bool>(type: "boolean", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendTradeCards", x => x.Id);
                    table.CheckConstraint("CK_FriendTradeCard_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_FriendTradeCards_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendTradeCards_FriendTrades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "FriendTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FriendMessages_FriendshipId_CreatedAt_Id",
                table: "FriendMessages",
                columns: new[] { "FriendshipId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_FirstUserId_SecondUserId",
                table: "Friendships",
                columns: new[] { "FirstUserId", "SecondUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_SecondUserId",
                table: "Friendships",
                column: "SecondUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendTradeCards_CardId",
                table: "FriendTradeCards",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendTradeCards_TradeId_CardId_IsOffered",
                table: "FriendTradeCards",
                columns: new[] { "TradeId", "CardId", "IsOffered" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendTrades_FriendshipId_CreatedAt",
                table: "FriendTrades",
                columns: new[] { "FriendshipId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FriendMessages");

            migrationBuilder.DropTable(
                name: "FriendTradeCards");

            migrationBuilder.DropTable(
                name: "FriendTrades");

            migrationBuilder.DropTable(
                name: "Friendships");
        }
    }
}
