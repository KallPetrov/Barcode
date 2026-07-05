using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CALAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinShelfLifeToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinShelfLifeDays",
                table: "Items",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinShelfLifeDays",
                table: "Items");
        }
    }
}
