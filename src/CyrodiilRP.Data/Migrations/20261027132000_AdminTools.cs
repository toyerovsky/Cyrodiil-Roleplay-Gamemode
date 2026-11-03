using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy" });

            // Their commands are gone, so no role keeps their names, a role made by hand included.
            migrationBuilder.Sql("""
                UPDATE staff_roles
                SET permissions = array_remove(array_remove(array_remove(array_remove(permissions, 'aname'), 'acreator'), 'aaccount'), 'aannounce')
                WHERE permissions && ARRAY['aname', 'acreator', 'aaccount', 'aannounce'];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "aheal", "agoto", "abring", "akick", "amute", "aunmute", "awhy", "aaccount", "aannounce", "agold", "aname", "acreator", "aban", "aunban", "aroles", "atrader" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "aheal", "agoto", "abring", "akick", "amute", "aunmute", "awhy", "aaccount", "aannounce" });
        }
    }
}
