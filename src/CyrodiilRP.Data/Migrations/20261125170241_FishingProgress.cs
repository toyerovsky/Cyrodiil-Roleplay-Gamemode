using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class FishingProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_fish_records",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    species = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    best_length_cm = table.Column<int>(type: "integer", nullable: false),
                    best_grams = table.Column<int>(type: "integer", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    best_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_fish_records", x => new { x.character_id, x.species });
                    table.CheckConstraint("ck_character_fish_records_best", "best_length_cm > 0 AND best_grams > 0 AND count > 0");
                    table.ForeignKey(
                        name: "fk_character_fish_records_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_fishing",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    xp = table.Column<int>(type: "integer", nullable: false),
                    catches = table.Column<int>(type: "integer", nullable: false),
                    bait = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_fishing", x => x.character_id);
                    table.CheckConstraint("ck_character_fishing_counts", "xp >= 0 AND catches >= 0");
                    table.CheckConstraint("ck_character_fishing_level", "level BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "fk_character_fishing_characters_character_id",
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
                name: "character_fish_records");

            migrationBuilder.DropTable(
                name: "character_fishing");
        }
    }
}
