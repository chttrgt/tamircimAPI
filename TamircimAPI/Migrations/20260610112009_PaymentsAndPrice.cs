using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TamircimAPI.Migrations
{
    /// <inheritdoc />
    public partial class PaymentsAndPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "RepairRecords",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    RepairRecordId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_RepairRecords_RepairRecordId",
                        column: x => x.RepairRecordId,
                        principalTable: "RepairRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedByUserId",
                table: "Payments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DeletedByUserId",
                table: "Payments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RepairRecordId",
                table: "Payments",
                column: "RepairRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_RepairRecordId",
                table: "Payments",
                columns: new[] { "TenantId", "RepairRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UpdatedByUserId",
                table: "Payments",
                column: "UpdatedByUserId");

            // --- Row-Level Security (RLS): tenant izolasyonunun 2. katmanı ---
            // Payments de tenant-owned iş verisidir → diğer tablolarla aynı politika
            // (bkz InitialCreate). app.tenant_id oturum değişkenini interceptor ayarlar:
            //   = -1 → güvenilir arka plan bypass'ı; > 0 → o tenant; 0/NULL → hiçbir satır.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Payments"" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ""Payments"" FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ""Payments""
                  USING (
                    NULLIF(current_setting('app.tenant_id', true), '')::int = -1
                    OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::int
                  )
                  WITH CHECK (
                    NULLIF(current_setting('app.tenant_id', true), '')::int = -1
                    OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::int
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "RepairRecords");
        }
    }
}
