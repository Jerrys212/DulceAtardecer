using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DulceAtardecer.Migrations
{
    /// <inheritdoc />
    public partial class AddReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status_CancelledAt",
                table: "Sales",
                columns: new[] { "Status", "CancelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status_PaidAt",
                table: "Sales",
                columns: new[] { "Status", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleModifications_ModifiedAt",
                table: "SaleModifications",
                column: "ModifiedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_Status_CancelledAt",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Status_PaidAt",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleModifications_ModifiedAt",
                table: "SaleModifications");
        }
    }
}
