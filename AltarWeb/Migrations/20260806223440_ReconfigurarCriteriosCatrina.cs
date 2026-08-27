using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class ReconfigurarCriteriosCatrina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Collar",
                table: "EvaluacionesCatrina");

            migrationBuilder.RenameColumn(
                name: "Zapatos",
                table: "EvaluacionesCatrina",
                newName: "Vestuario");

            migrationBuilder.RenameColumn(
                name: "Vestimenta",
                table: "EvaluacionesCatrina",
                newName: "OriginalidadCreatividad");

            migrationBuilder.RenameColumn(
                name: "SombreroTocado",
                table: "EvaluacionesCatrina",
                newName: "ApegoTradiciones");

            migrationBuilder.RenameColumn(
                name: "Guantes",
                table: "EvaluacionesCatrina",
                newName: "Accesorios");

            migrationBuilder.AddColumn<decimal>(
                name: "PesoCatrinaAccesorios",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoCatrinaApegoTradiciones",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoCatrinaMaquillaje",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoCatrinaOriginalidadCreatividad",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoCatrinaVestuario",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPeriodo",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PesoCatrinaAccesorios", "PesoCatrinaApegoTradiciones", "PesoCatrinaMaquillaje", "PesoCatrinaOriginalidadCreatividad", "PesoCatrinaVestuario" },
                values: new object[] { 0.10m, 0.25m, 0.20m, 0.20m, 0.25m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PesoCatrinaAccesorios",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.DropColumn(
                name: "PesoCatrinaApegoTradiciones",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.DropColumn(
                name: "PesoCatrinaMaquillaje",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.DropColumn(
                name: "PesoCatrinaOriginalidadCreatividad",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.DropColumn(
                name: "PesoCatrinaVestuario",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.RenameColumn(
                name: "Vestuario",
                table: "EvaluacionesCatrina",
                newName: "Zapatos");

            migrationBuilder.RenameColumn(
                name: "OriginalidadCreatividad",
                table: "EvaluacionesCatrina",
                newName: "Vestimenta");

            migrationBuilder.RenameColumn(
                name: "ApegoTradiciones",
                table: "EvaluacionesCatrina",
                newName: "SombreroTocado");

            migrationBuilder.RenameColumn(
                name: "Accesorios",
                table: "EvaluacionesCatrina",
                newName: "Guantes");

            migrationBuilder.AddColumn<decimal>(
                name: "Collar",
                table: "EvaluacionesCatrina",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
