using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catrinas.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class RenombrarCarreraBioingenieria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Carreras",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Ing. en Bioingeniería");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Carreras",
                keyColumn: "Id",
                keyValue: 2,
                column: "Nombre",
                value: "Bioingeniero");
        }
    }
}
