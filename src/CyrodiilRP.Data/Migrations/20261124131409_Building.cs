using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class Building : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    first_tile_x = table.Column<int>(type: "integer", nullable: false),
                    first_tile_y = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    ground_z = table.Column<float>(type: "real", nullable: false),
                    height = table.Column<float>(type: "real", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    weekly_tax = table.Column<int>(type: "integer", nullable: false),
                    owner_character_id = table.Column<long>(type: "bigint", nullable: true),
                    sign_x = table.Column<float>(type: "real", nullable: false),
                    sign_y = table.Column<float>(type: "real", nullable: false),
                    sign_z = table.Column<float>(type: "real", nullable: false),
                    sign_yaw = table.Column<float>(type: "real", nullable: false),
                    made_by_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    made_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plots", x => x.id);
                    table.CheckConstraint("ck_plots_gold", "price >= 0 AND weekly_tax >= 0");
                    table.CheckConstraint("ck_plots_size", "width > 0 AND depth > 0 AND height > 0");
                    table.ForeignKey(
                        name: "fk_plots_characters_owner_character_id",
                        column: x => x.owner_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "plot_objects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plot_id = table.Column<long>(type: "bigint", nullable: false),
                    piece = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false),
                    pitch = table.Column<float>(type: "real", nullable: false),
                    yaw = table.Column<float>(type: "real", nullable: false),
                    roll = table.Column<float>(type: "real", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    placed_by_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plot_objects", x => x.id);
                    table.CheckConstraint("ck_plot_objects_piece", "length(piece) > 0");
                    table.ForeignKey(
                        name: "fk_plot_objects_plots_plot_id",
                        column: x => x.plot_id,
                        principalTable: "plots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agamble", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction", "aboard", "aspec", "acap", "astat", "atable", "aauction", "aplot", "abuild" });

            migrationBuilder.CreateIndex(
                name: "ix_plot_objects_plot_id",
                table: "plot_objects",
                column: "plot_id");

            migrationBuilder.CreateIndex(
                name: "ix_plots_area",
                table: "plots",
                column: "area");

            migrationBuilder.CreateIndex(
                name: "ix_plots_owner_character_id",
                table: "plots",
                column: "owner_character_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plot_objects");

            migrationBuilder.DropTable(
                name: "plots");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "name",
                keyValue: "admin",
                column: "permissions",
                value: new[] { "a", "duty", "admin", "aheal", "agoto", "abring", "apos", "atp", "aback", "arevive", "akick", "amute", "aunmute", "afreeze", "aunfreeze", "awarn", "awhy", "amsg", "ahp", "alore", "astep", "afly", "afeed", "anotice", "agamble", "agold", "aitem", "atime", "aweather", "aban", "aunban", "aroles", "anpc", "aspawn", "afaction", "aboard", "aspec", "acap", "astat", "atable", "aauction" });
        }
    }
}
