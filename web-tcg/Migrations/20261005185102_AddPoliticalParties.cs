using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class AddPoliticalParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PoliticalPartyId",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PoliticalParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CountryId = table.Column<int>(type: "integer", nullable: false),
                    Orientation = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticalParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoliticalParties_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_PoliticalPartyId",
                table: "Cards",
                column: "PoliticalPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticalParties_CountryId_Name",
                table: "PoliticalParties",
                columns: new[] { "CountryId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_PoliticalParties_PoliticalPartyId",
                table: "Cards",
                column: "PoliticalPartyId",
                principalTable: "PoliticalParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_PoliticalParties_PoliticalPartyId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "PoliticalParties");

            migrationBuilder.DropIndex(
                name: "IX_Cards_PoliticalPartyId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PoliticalPartyId",
                table: "Cards");
        }
    }
}
