using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCardVariantsAsCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCardCollections_CardVariants_CardVariantId",
                table: "UserCardCollections");

            migrationBuilder.DropTable(
                name: "CardVariants");

            migrationBuilder.DropIndex(
                name: "IX_UserCardCollections_CardVariantId",
                table: "UserCardCollections");

            migrationBuilder.DropIndex(
                name: "IX_UserCardCollections_UserId_CardId_CardVariantId_Condition",
                table: "UserCardCollections");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardSetId_RegionId_CollectionNumber",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardVariantId",
                table: "UserCardCollections");

            migrationBuilder.AddColumn<int>(
                name: "ArtType",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BaseCardId",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VariantNumber",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BaseCardId_VariantNumber",
                table: "Cards",
                columns: new[] { "BaseCardId", "VariantNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardSetId_RegionId_CollectionNumber",
                table: "Cards",
                columns: new[] { "CardSetId", "RegionId", "CollectionNumber" },
                unique: true,
                filter: "\"BaseCardId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Cards_BaseCardId",
                table: "Cards",
                column: "BaseCardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Cards_BaseCardId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_BaseCardId_VariantNumber",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardSetId_RegionId_CollectionNumber",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ArtType",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BaseCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "VariantNumber",
                table: "Cards");

            migrationBuilder.AddColumn<int>(
                name: "CardVariantId",
                table: "UserCardCollections",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    ArtType = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VariantNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardVariants_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCardCollections_CardVariantId",
                table: "UserCardCollections",
                column: "CardVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCardCollections_UserId_CardId_CardVariantId_Condition",
                table: "UserCardCollections",
                columns: new[] { "UserId", "CardId", "CardVariantId", "Condition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardSetId_RegionId_CollectionNumber",
                table: "Cards",
                columns: new[] { "CardSetId", "RegionId", "CollectionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CardVariants_CardId_VariantNumber",
                table: "CardVariants",
                columns: new[] { "CardId", "VariantNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCardCollections_CardVariants_CardVariantId",
                table: "UserCardCollections",
                column: "CardVariantId",
                principalTable: "CardVariants",
                principalColumn: "Id");
        }
    }
}
