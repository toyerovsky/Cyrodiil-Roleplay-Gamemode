using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacterProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_progression",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    xp = table.Column<int>(type: "integer", nullable: false),
                    perk_points = table.Column<int>(type: "integer", nullable: false),
                    reached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    gathering_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    gathering_xp = table.Column<int>(type: "integer", nullable: false),
                    skill_peaks = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_progression", x => x.character_id);
                    table.CheckConstraint("ck_character_progression_counts", "xp >= 0 AND perk_points >= 0 AND gathering_xp >= 0");
                    table.CheckConstraint("ck_character_progression_level", "level BETWEEN 1 AND 65535");
                    table.ForeignKey(
                        name: "fk_character_progression_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE staff_roles SET permissions = array_append(permissions, 'axp')
                WHERE name = 'admin' AND NOT ('axp' = ANY(permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_progression");

            migrationBuilder.Sql("""
                UPDATE staff_roles SET permissions = array_remove(permissions, 'axp')
                WHERE name = 'admin';
                """);
        }
    }
}
