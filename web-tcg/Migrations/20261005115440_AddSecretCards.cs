using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class AddSecretCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Cards_SecretPerSet",
                table: "Cards",
                column: "CardSetId",
                unique: true,
                filter: "\"CollectionNumber\" = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cards_Secret",
                table: "Cards",
                sql: "(\"Type\" = 5 AND \"CollectionNumber\" = 0 AND \"Rarity\" = 5 AND \"Rarity\" IS NOT NULL AND \"UsageCost\" = 0 AND \"BaseCardId\" IS NULL AND \"VariantNumber\" IS NULL) OR (\"Type\" <> 5 AND \"CollectionNumber\" > 0 AND (\"Rarity\" IS NULL OR \"Rarity\" <> 5))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_SecretPerSet",
                table: "Cards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cards_Secret",
                table: "Cards");
        }
    }
}
