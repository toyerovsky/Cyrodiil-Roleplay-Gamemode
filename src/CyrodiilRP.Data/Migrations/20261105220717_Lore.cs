using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "name_public",
                table: "characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "lore_fields",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    field_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    catalog_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    number = table.Column<int>(type: "integer", nullable: true),
                    @fixed = table.Column<bool>(name: "fixed", type: "boolean", nullable: false),
                    rolls = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lore_fields", x => new { x.character_id, x.field_id });
                    table.CheckConstraint("ck_lore_fields_number", "number IS NULL OR number BETWEEN 5 AND 100");
                    table.ForeignKey(
                        name: "fk_lore_fields_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lore_introductions",
                columns: table => new
                {
                    knower_character_id = table.Column<long>(type: "bigint", nullable: false),
                    known_character_id = table.Column<long>(type: "bigint", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lore_introductions", x => new { x.knower_character_id, x.known_character_id });
                    table.CheckConstraint("ck_lore_introductions_other", "knower_character_id <> known_character_id");
                    table.ForeignKey(
                        name: "fk_lore_introductions_characters_knower_character_id",
                        column: x => x.knower_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lore_introductions_characters_known_character_id",
                        column: x => x.known_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader", "aspawn" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore" });

            migrationBuilder.CreateIndex(
                name: "ix_lore_introductions_known_character_id",
                table: "lore_introductions",
                column: "known_character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lore_fields");

            migrationBuilder.DropTable(
                name: "lore_introductions");

            migrationBuilder.DropColumn(
                name: "name_public",
                table: "characters");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader", "aspawn" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp" });
        }
    }
}
