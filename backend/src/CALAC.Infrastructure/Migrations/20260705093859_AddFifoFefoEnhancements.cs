using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CALAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFifoFefoEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TransferOrderLines",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TransferOrderLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "TransferOrderLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "SyncOperations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PickingStockLines",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsOverride",
                table: "PickingStockLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OverrideReason",
                table: "PickingStockLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PickingStockLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PickingStockLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPickingStrategy",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BestBeforeDate",
                table: "InventoryStocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionDate",
                table: "InventoryStocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptDate",
                table: "InventoryStocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "InventoryStocks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BestBeforeDate",
                table: "InventoryCounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionDate",
                table: "InventoryCounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BestBeforeDate",
                table: "GoodsReceiptLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionDate",
                table: "GoodsReceiptLines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourierConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CourierType = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ClientNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourierConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceStores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PlatformType = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ConsumerKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConsumerSecret = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WebhookSecret = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AutoImportOrders = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoSyncStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceStores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcommerceStores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartnerApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerApiKeys_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantBrandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrimaryColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SecondaryColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FaviconUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WelcomeMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBrandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBrandings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "TEXT", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaveBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OrdersJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaveBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaveBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Secret = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSuccessAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PickingOrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CourierConfigurationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiverName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReceiverPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReceiverEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReceiverAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReceiverCity = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReceiverPostCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PackageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    IsCashOnDelivery = table.Column<bool>(type: "INTEGER", nullable: false),
                    CodAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    WaybillNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LabelPdfUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LabelZpl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TrackingUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ShippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_CourierConfigurations_CourierConfigurationId",
                        column: x => x.CourierConfigurationId,
                        principalTable: "CourierConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Shipments_PickingOrders_PickingOrderId",
                        column: x => x.PickingOrderId,
                        principalTable: "PickingOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Shipments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EcommerceStoreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PickingOrderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShippingAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ShippingCity = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    RawJson = table.Column<string>(type: "TEXT", nullable: true),
                    OrderCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_EcommerceStores_EcommerceStoreId",
                        column: x => x.EcommerceStoreId,
                        principalTable: "EcommerceStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_PickingOrders_PickingOrderId",
                        column: x => x.PickingOrderId,
                        principalTable: "PickingOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EcommerceOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_DeviceId_IdempotencyKey",
                table: "SyncOperations",
                columns: new[] { "DeviceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourierConfigurations_TenantId_CourierType",
                table: "CourierConfigurations",
                columns: new[] { "TenantId", "CourierType" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_EcommerceStoreId",
                table: "EcommerceOrders",
                column: "EcommerceStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_PickingOrderId",
                table: "EcommerceOrders",
                column: "PickingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_Status",
                table: "EcommerceOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceOrders_TenantId_ExternalOrderId",
                table: "EcommerceOrders",
                columns: new[] { "TenantId", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceStores_TenantId_PlatformType",
                table: "EcommerceStores",
                columns: new[] { "TenantId", "PlatformType" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiKeys_TenantId_IsActive",
                table: "PartnerApiKeys",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiKeys_TenantId_Key",
                table: "PartnerApiKeys",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CourierConfigurationId",
                table: "Shipments",
                column: "CourierConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_PickingOrderId",
                table: "Shipments",
                column: "PickingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId_Status",
                table: "Shipments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_WaybillNumber",
                table: "Shipments",
                column: "WaybillNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_TenantId",
                table: "TenantBrandings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_TenantId_PlanCode",
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "PlanCode" });

            migrationBuilder.CreateIndex(
                name: "IX_WaveBatches_TenantId_Name",
                table: "WaveBatches",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_TenantId_EventType",
                table: "WebhookSubscriptions",
                columns: new[] { "TenantId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_TenantId_IsActive",
                table: "WebhookSubscriptions",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EcommerceOrders");

            migrationBuilder.DropTable(
                name: "PartnerApiKeys");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "TenantBrandings");

            migrationBuilder.DropTable(
                name: "TenantSubscriptions");

            migrationBuilder.DropTable(
                name: "WaveBatches");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions");

            migrationBuilder.DropTable(
                name: "EcommerceStores");

            migrationBuilder.DropTable(
                name: "CourierConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_SyncOperations_DeviceId_IdempotencyKey",
                table: "SyncOperations");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TransferOrderLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TransferOrderLines");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "TransferOrderLines");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "SyncOperations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PickingStockLines");

            migrationBuilder.DropColumn(
                name: "IsOverride",
                table: "PickingStockLines");

            migrationBuilder.DropColumn(
                name: "OverrideReason",
                table: "PickingStockLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PickingStockLines");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PickingStockLines");

            migrationBuilder.DropColumn(
                name: "DefaultPickingStrategy",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "BestBeforeDate",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "ProductionDate",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "ReceiptDate",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "BestBeforeDate",
                table: "InventoryCounts");

            migrationBuilder.DropColumn(
                name: "ProductionDate",
                table: "InventoryCounts");

            migrationBuilder.DropColumn(
                name: "BestBeforeDate",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "ProductionDate",
                table: "GoodsReceiptLines");
        }
    }
}
