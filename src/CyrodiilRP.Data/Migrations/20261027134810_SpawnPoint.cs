using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpawnPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spawn_point",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    area = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false),
                    yaw = table.Column<float>(type: "real", nullable: false),
                    set_by_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spawn_point", x => x.id);
                    table.CheckConstraint("ck_spawn_point_one", "id = 1");
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader", "aspawn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spawn_point");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader" });
        }
    }
}
