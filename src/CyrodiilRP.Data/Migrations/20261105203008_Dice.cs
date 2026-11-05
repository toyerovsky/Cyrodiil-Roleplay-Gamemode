using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Dice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dice_set",
                table: "characters",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dice_rolls",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    character_id = table.Column<long>(type: "bigint", nullable: false),
                    character_name = table.Column<string>(type: "character varying(81)", maxLength: 81, nullable: false),
                    asked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dice = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    area = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dice_rolls", x => x.id);
                    table.CheckConstraint("ck_dice_rolls_total", "total > 0");
                    table.ForeignKey(
                        name: "fk_dice_rolls_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dice_rolls_character_id_id",
                table: "dice_rolls",
                columns: new[] { "character_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dice_rolls");

            migrationBuilder.DropColumn(
                name: "dice_set",
                table: "characters");
        }
    }
}
