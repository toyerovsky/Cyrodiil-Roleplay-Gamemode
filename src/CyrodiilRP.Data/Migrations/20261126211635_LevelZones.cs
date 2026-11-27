using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyrodiilRP.Data.Migrations
{
    /// <inheritdoc />
    public partial class LevelZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE staff_roles
                SET permissions = array_append(permissions, 'azone')
                WHERE name = 'admin' AND NOT ('azone' = ANY(permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE staff_roles SET permissions = array_remove(permissions, 'azone')
                WHERE name = 'admin';
                """);
        }
    }
}
