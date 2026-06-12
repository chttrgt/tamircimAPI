using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TamircimAPI.Migrations
{
    /// <inheritdoc />
    public partial class AccountDeletionScheduledAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionScheduledAt",
                table: "Tenants",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionScheduledAt",
                table: "Tenants");
        }
    }
}
