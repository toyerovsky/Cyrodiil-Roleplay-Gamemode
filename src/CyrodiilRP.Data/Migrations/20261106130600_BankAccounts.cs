using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class BankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    balance = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_accounts", x => x.character_id);
                    table.CheckConstraint("ck_bank_accounts_balance", "balance >= 0");
                    table.ForeignKey(
                        name: "fk_bank_accounts_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction" });

            // The roles the staff made themselves keep the command under its new name.
            migrationBuilder.Sql("UPDATE staff_roles SET permissions = array_replace(permissions, 'atrader', 'anpc');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE staff_roles SET permissions = array_replace(permissions, 'anpc', 'atrader');");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "atrader", "aspawn", "afaction" });
        }
    }
}
