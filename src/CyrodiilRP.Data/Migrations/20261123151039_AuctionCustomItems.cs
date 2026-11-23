using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuctionCustomItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_item",
                table: "auction_lots",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_auction_lots_custom_item",
                table: "auction_lots",
                sql: "(custom_item IS NULL AND form <> 0) OR (custom_item IS NOT NULL AND form = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_auction_lots_custom_item",
                table: "auction_lots");

            migrationBuilder.DropColumn(
                name: "custom_item",
                table: "auction_lots");
        }
    }
}
