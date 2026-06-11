using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TamircimAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPaidAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_PaidAt",
                table: "Payments",
                columns: new[] { "TenantId", "PaidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_PaidAt",
                table: "Payments");
        }
    }
}
