using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class StableHorses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_horses",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    horse = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    bought_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_horses", x => x.character_id);
                    table.CheckConstraint("ck_character_horses_price", "price >= 0");
                    table.ForeignKey(
                        name: "fk_character_horses_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_horses");
        }
    }
}
