using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CALAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinShelfLifePercentageToPickingOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinRemainingShelfLifePercentage",
                table: "PickingOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinRemainingShelfLifePercentage",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinRemainingShelfLifePercentage",
                table: "InventorySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinRemainingShelfLifePercentage",
                table: "InventoryCounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinRemainingShelfLifePercentage",
                table: "PickingOrders");

            migrationBuilder.DropColumn(
                name: "MinRemainingShelfLifePercentage",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "MinRemainingShelfLifePercentage",
                table: "InventorySessions");

            migrationBuilder.DropColumn(
                name: "MinRemainingShelfLifePercentage",
                table: "InventoryCounts");
        }
    }
}
