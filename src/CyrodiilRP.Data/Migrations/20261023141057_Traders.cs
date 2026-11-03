using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Traders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trader_spots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    area = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    cell = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false),
                    yaw = table.Column<float>(type: "real", nullable: false),
                    npc_ref_id = table.Column<long>(type: "bigint", nullable: false),
                    placed_by_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trader_spots", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "aheal", "agoto", "abring", "akick", "amute", "aunmute", "awhy", "aaccount", "aannounce", "agold", "aname", "acreator", "aban", "aunban", "aroles", "atrader" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trader_spots");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "aheal", "agoto", "abring", "akick", "amute", "aunmute", "awhy", "aaccount", "aannounce", "agold", "aname", "acreator", "aban", "aunban", "aroles" });
        }
    }
}
