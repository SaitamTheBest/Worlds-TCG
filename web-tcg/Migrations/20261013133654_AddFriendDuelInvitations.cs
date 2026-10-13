using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_tcg.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendDuelInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FriendDuelInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FriendshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterId = table.Column<string>(type: "text", nullable: false),
                    RecipientId = table.Column<string>(type: "text", nullable: false),
                    InviterDeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientDeckId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendDuelInvitations", x => x.Id);
                    table.CheckConstraint("CK_FriendDuel_DifferentPlayers", "\"InviterId\" <> \"RecipientId\"");
                    table.ForeignKey(
                        name: "FK_FriendDuelInvitations_AspNetUsers_InviterId",
                        column: x => x.InviterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendDuelInvitations_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendDuelInvitations_Friendships_FriendshipId",
                        column: x => x.FriendshipId,
                        principalTable: "Friendships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendDuelInvitations_GameMatch_MatchId",
                        column: x => x.MatchId,
                        principalTable: "GameMatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FriendDuelInvitations_FriendshipId",
                table: "FriendDuelInvitations",
                column: "FriendshipId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendDuelInvitations_InviterId",
                table: "FriendDuelInvitations",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendDuelInvitations_MatchId",
                table: "FriendDuelInvitations",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendDuelInvitations_RecipientId",
                table: "FriendDuelInvitations",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendDuelInvitations_Status_ExpiresAt",
                table: "FriendDuelInvitations",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FriendDuelInvitations");
        }
    }
}
