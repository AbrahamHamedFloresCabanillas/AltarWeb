using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUmbralAgrupacionDemografica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UmbralAgrupacionDemografica",
                table: "ConfiguracionesPeriodo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPeriodo",
                keyColumn: "Id",
                keyValue: 1,
                column: "UmbralAgrupacionDemografica",
                value: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UmbralAgrupacionDemografica",
                table: "ConfiguracionesPeriodo");
        }
    }
}
