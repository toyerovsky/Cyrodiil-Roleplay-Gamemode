using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction" });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "moderator",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
