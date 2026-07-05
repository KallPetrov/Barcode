using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CALAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchGenealogy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProducedInventoryStockId",
                table: "WorkOrderConsumptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderConsumptions_ProducedInventoryStockId",
                table: "WorkOrderConsumptions",
                column: "ProducedInventoryStockId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderConsumptions_InventoryStocks_ProducedInventoryStockId",
                table: "WorkOrderConsumptions",
                column: "ProducedInventoryStockId",
                principalTable: "InventoryStocks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderConsumptions_InventoryStocks_ProducedInventoryStockId",
                table: "WorkOrderConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderConsumptions_ProducedInventoryStockId",
                table: "WorkOrderConsumptions");

            migrationBuilder.DropColumn(
                name: "ProducedInventoryStockId",
                table: "WorkOrderConsumptions");
        }
    }
}
