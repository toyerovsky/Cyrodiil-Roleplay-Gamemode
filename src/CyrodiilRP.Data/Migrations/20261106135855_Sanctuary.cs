using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sanctuary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sanctuary_incidents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    area = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    area_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    victim_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    victim_account = table.Column<Guid>(type: "uuid", nullable: true),
                    victim_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    damage = table.Column<float>(type: "real", nullable: false),
                    died = table.Column<bool>(type: "boolean", nullable: false),
                    verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    aggressors = table.Column<string[]>(type: "text[]", nullable: false),
                    aggressor_accounts = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sanctuary_incidents", x => x.id);
                    table.CheckConstraint("ck_sanctuary_incidents_aggressors", "cardinality(aggressors) = cardinality(aggressor_accounts)");
                    table.CheckConstraint("ck_sanctuary_incidents_damage", "damage >= 0");
                    table.CheckConstraint("ck_sanctuary_incidents_victim", "(victim_kind = 'Player') = (victim_account IS NOT NULL)");
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "asanctuary", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "asanctuary" });

            migrationBuilder.CreateIndex(
                name: "ix_sanctuary_incidents_at",
                table: "sanctuary_incidents",
                column: "at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sanctuary_incidents");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly" });
        }
    }
}
