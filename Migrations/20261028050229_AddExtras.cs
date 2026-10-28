using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DulceAtardecer.Migrations
{
    /// <inheritdoc />
    public partial class AddExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Extras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "citext", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedById = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Extras", x => x.Id);
                    table.CheckConstraint("CK_Extras_Name_Length", "char_length(\"Name\") BETWEEN 2 AND 50");
                    table.CheckConstraint("CK_Extras_Price_Range", "\"Price\" BETWEEN 0 AND 9999.99");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Extras_Name",
                table: "Extras",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Extras");
        }
    }
}
