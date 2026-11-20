using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Gambling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gambling_tables",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cell = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false),
                    yaw = table.Column<float>(type: "real", nullable: false),
                    placed_by_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gambling_tables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gambling_games",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    table_id = table.Column<long>(type: "bigint", nullable: false),
                    first_character_id = table.Column<long>(type: "bigint", nullable: false),
                    second_character_id = table.Column<long>(type: "bigint", nullable: false),
                    first_name = table.Column<string>(type: "character varying(81)", maxLength: 81, nullable: false),
                    second_name = table.Column<string>(type: "character varying(81)", maxLength: 81, nullable: false),
                    stake = table.Column<int>(type: "integer", nullable: false),
                    first_raises = table.Column<int>(type: "integer", nullable: false),
                    second_raises = table.Column<int>(type: "integer", nullable: false),
                    pot = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    winner_character_id = table.Column<long>(type: "bigint", nullable: true),
                    cut = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rounds = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gambling_games", x => x.id);
                    table.CheckConstraint("ck_gambling_games_gold", "stake > 0 AND first_raises >= 0 AND second_raises >= 0 AND pot::bigint = 2::bigint * stake + first_raises + second_raises AND cut >= 0 AND cut <= pot");
                    table.CheckConstraint("ck_gambling_games_players", "first_character_id <> second_character_id");
                    table.CheckConstraint("ck_gambling_games_status", "(status = 'Open' AND ended_at IS NULL AND winner_character_id IS NULL AND cut = 0) OR (status = 'Settled' AND ended_at IS NOT NULL AND winner_character_id IS NOT NULL) OR (status IN ('Refunded', 'Cancelled') AND ended_at IS NOT NULL AND winner_character_id IS NULL AND cut = 0)");
                    table.CheckConstraint("ck_gambling_games_winner", "winner_character_id IS NULL OR winner_character_id IN (first_character_id, second_character_id)");
                    table.ForeignKey(
                        name: "fk_gambling_games_characters_first_character_id",
                        column: x => x.first_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gambling_games_characters_second_character_id",
                        column: x => x.second_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gambling_games_gambling_tables_table_id",
                        column: x => x.table_id,
                        principalTable: "gambling_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agamble", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction", "aboard", "aspec", "acap", "astat", "atable" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agamble" });

            migrationBuilder.CreateIndex(
                name: "ix_gambling_games_first_character_id_id",
                table: "gambling_games",
                columns: new[] { "first_character_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_gambling_games_operation_id",
                table: "gambling_games",
                column: "operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gambling_games_second_character_id_id",
                table: "gambling_games",
                columns: new[] { "second_character_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_gambling_games_status",
                table: "gambling_games",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_gambling_games_table_id",
                table: "gambling_games",
                column: "table_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gambling_games");

            migrationBuilder.DropTable(
                name: "gambling_tables");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction", "aboard", "aspec", "acap", "astat" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice" });
        }
    }
}
