using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacterSpecializations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_specializations",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    specialization = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    chosen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_specializations", x => x.character_id);
                    table.CheckConstraint("ck_character_specializations_changes", "changes >= 0");
                    table.CheckConstraint("ck_character_specializations_price", "price >= 0");
                    table.ForeignKey(
                        name: "fk_character_specializations_characters_character_id",
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
                name: "character_specializations");
        }
    }
}
