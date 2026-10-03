using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class AjoutUserEtBooster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCardCollections_Cards_CardId",
                table: "UserCardCollections");

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "AspNetUsers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "XP",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BoosterOpenings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PricePaid = table.Column<decimal>(type: "numeric", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoosterOpenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoosterOpenings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoosterOpeningCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoosterOpeningId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    Rarity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsRevealed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoosterOpeningCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoosterOpeningCards_BoosterOpenings_BoosterOpeningId",
                        column: x => x.BoosterOpeningId,
                        principalTable: "BoosterOpenings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoosterOpeningCards_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCardCollections_UserId_CardId_CardVariantId_Condition",
                table: "UserCardCollections",
                columns: new[] { "UserId", "CardId", "CardVariantId", "Condition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpeningCards_BoosterOpeningId",
                table: "BoosterOpeningCards",
                column: "BoosterOpeningId");

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpeningCards_CardId",
                table: "BoosterOpeningCards",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpenings_UserId",
                table: "BoosterOpenings",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCardCollections_Cards_CardId",
                table: "UserCardCollections",
                column: "CardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCardCollections_Cards_CardId",
                table: "UserCardCollections");

            migrationBuilder.DropTable(
                name: "BoosterOpeningCards");

            migrationBuilder.DropTable(
                name: "BoosterOpenings");

            migrationBuilder.DropIndex(
                name: "IX_UserCardCollections_UserId_CardId_CardVariantId_Condition",
                table: "UserCardCollections");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "XP",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCardCollections_Cards_CardId",
                table: "UserCardCollections",
                column: "CardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
