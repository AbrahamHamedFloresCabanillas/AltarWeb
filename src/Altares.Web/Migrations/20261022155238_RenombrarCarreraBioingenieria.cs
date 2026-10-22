using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class RenombrarCarreraBioingenieria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CatalogoCarreras",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Ing. en Bioingeniería");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CatalogoCarreras",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Bioingeniero");
        }
    }
}
