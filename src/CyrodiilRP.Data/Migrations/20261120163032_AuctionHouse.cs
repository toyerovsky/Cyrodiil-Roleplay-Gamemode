using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuctionHouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auction_lots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seller_character_id = table.Column<long>(type: "bigint", nullable: false),
                    seller_name = table.Column<string>(type: "character varying(81)", maxLength: 81, nullable: false),
                    form = table.Column<long>(type: "bigint", nullable: false),
                    item_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    item_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    health = table.Column<float>(type: "real", nullable: false),
                    charge = table.Column<float>(type: "real", nullable: false),
                    start_price = table.Column<int>(type: "integer", nullable: true),
                    buyout_price = table.Column<int>(type: "integer", nullable: true),
                    fee = table.Column<int>(type: "integer", nullable: false),
                    top_bid = table.Column<int>(type: "integer", nullable: true),
                    top_bidder_character_id = table.Column<long>(type: "bigint", nullable: true),
                    bids = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    buyer_character_id = table.Column<long>(type: "bigint", nullable: true),
                    price = table.Column<int>(type: "integer", nullable: true),
                    cut = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_lots", x => x.id);
                    table.CheckConstraint("ck_auction_lots_bid", "bids >= 0 AND (top_bid IS NULL) = (top_bidder_character_id IS NULL) AND (top_bid IS NULL) = (bids = 0) AND (top_bid IS NULL OR (start_price IS NOT NULL AND top_bid >= start_price AND (buyout_price IS NULL OR top_bid < buyout_price))) AND (top_bidder_character_id IS NULL OR top_bidder_character_id <> seller_character_id)");
                    table.CheckConstraint("ck_auction_lots_item", "count > 0");
                    table.CheckConstraint("ck_auction_lots_prices", "(start_price IS NOT NULL OR buyout_price IS NOT NULL) AND (start_price IS NULL OR start_price > 0) AND (buyout_price IS NULL OR buyout_price > 0) AND (start_price IS NULL OR buyout_price IS NULL OR buyout_price > start_price) AND fee >= 0");
                    table.CheckConstraint("ck_auction_lots_status", "(status = 'Open' AND closed_at IS NULL AND buyer_character_id IS NULL AND price IS NULL AND cut = 0) OR (status = 'Sold' AND closed_at IS NOT NULL AND buyer_character_id IS NOT NULL AND buyer_character_id <> seller_character_id AND price > 0 AND cut >= 0 AND cut <= price) OR (status IN ('Expired', 'Withdrawn', 'Removed') AND closed_at IS NOT NULL AND buyer_character_id IS NULL AND price IS NULL AND cut = 0)");
                    table.ForeignKey(
                        name: "fk_auction_lots_characters_buyer_character_id",
                        column: x => x.buyer_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auction_lots_characters_seller_character_id",
                        column: x => x.seller_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auction_lots_characters_top_bidder_character_id",
                        column: x => x.top_bidder_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_parcels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    lot_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    gold = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_parcels", x => x.id);
                    table.CheckConstraint("ck_auction_parcels_gold", "(reason IN ('Sale', 'Outbid', 'Refund') AND gold > 0) OR (reason IN ('Won', 'Bought', 'Unsold', 'Withdrawn', 'Removed') AND gold = 0)");
                    table.ForeignKey(
                        name: "fk_auction_parcels_auction_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "auction_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auction_parcels_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agamble", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction", "aboard", "aspec", "acap", "astat", "atable", "aauction" });

            migrationBuilder.CreateIndex(
                name: "ix_auction_lots_buyer_character_id",
                table: "auction_lots",
                column: "buyer_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_lots_seller_character_id_status",
                table: "auction_lots",
                columns: new[] { "seller_character_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_auction_lots_status_ends_at",
                table: "auction_lots",
                columns: new[] { "status", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_auction_lots_top_bidder_character_id",
                table: "auction_lots",
                column: "top_bidder_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_parcels_character_id",
                table: "auction_parcels",
                column: "character_id",
                filter: "collected_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_auction_parcels_lot_id",
                table: "auction_parcels",
                column: "lot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auction_parcels");

            migrationBuilder.DropTable(
                name: "auction_lots");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agamble", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction", "aboard", "aspec", "acap", "astat", "atable" });
        }
    }
}
