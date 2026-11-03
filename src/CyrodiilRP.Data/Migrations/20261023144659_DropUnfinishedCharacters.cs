using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropUnfinishedCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A character is written only once the game's creator has closed. Those left half made go, and their
            // conditions and owed gold with them through their cascading foreign keys.
            migrationBuilder.Sql("DELETE FROM characters WHERE NOT appearance_done;");

            migrationBuilder.DropColumn(
                name: "appearance_done",
                table: "characters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "appearance_done",
                table: "characters",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
