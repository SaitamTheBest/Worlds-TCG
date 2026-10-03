using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class AddGeographicAbilityConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParametersJson",
                table: "CardAbilityCondition",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParametersJson",
                table: "CardAbilityCondition");
        }
    }
}
