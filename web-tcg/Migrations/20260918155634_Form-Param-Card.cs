using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class FormParamCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CardSets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CardSets");
        }
    }
}
