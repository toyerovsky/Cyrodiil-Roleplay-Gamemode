using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Factions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "factions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    founded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_factions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faction_ranks",
                columns: table => new
                {
                    faction_id = table.Column<long>(type: "bigint", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faction_ranks", x => new { x.faction_id, x.number });
                    table.CheckConstraint("ck_faction_ranks_number", "number > 0");
                    table.CheckConstraint("ck_faction_ranks_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_faction_ranks_factions_faction_id",
                        column: x => x.faction_id,
                        principalTable: "factions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faction_members",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    faction_id = table.Column<long>(type: "bigint", nullable: false),
                    rank_number = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faction_members", x => x.character_id);
                    table.ForeignKey(
                        name: "fk_faction_members_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_faction_members_faction_ranks_faction_id_rank_number",
                        columns: x => new { x.faction_id, x.rank_number },
                        principalTable: "faction_ranks",
                        principalColumns: new[] { "faction_id", "number" });
                    table.ForeignKey(
                        name: "fk_faction_members_factions_faction_id",
                        column: x => x.faction_id,
                        principalTable: "factions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader", "aspawn", "afaction" });

            migrationBuilder.CreateIndex(
                name: "ix_faction_members_faction_id_rank_number",
                table: "faction_members",
                columns: new[] { "faction_id", "rank_number" });

            migrationBuilder.CreateIndex(
                name: "ix_factions_name_key",
                table: "factions",
                column: "name_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "faction_members");

            migrationBuilder.DropTable(
                name: "faction_ranks");

            migrationBuilder.DropTable(
                name: "factions");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader", "aspawn" });
        }
    }
}
