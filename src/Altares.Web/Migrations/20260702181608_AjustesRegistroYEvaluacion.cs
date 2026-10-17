using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AjustesRegistroYEvaluacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MaestroEncargadoId",
                table: "EquiposConcurso",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "MaestroEncargadoIdentificadorPendiente",
                table: "EquiposConcurso",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NivelAsignado",
                table: "ElementosEvaluados",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Tematizado",
                table: "ElementosEvaluados",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BonusPorElementoTematizado",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ExigirMinimoUnAnioFallecimiento",
                table: "ConfiguracionesPeriodo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPeriodo",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BonusPorElementoTematizado", "ExigirMinimoUnAnioFallecimiento" },
                values: new object[] { 0.25m, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaestroEncargadoIdentificadorPendiente",
                table: "EquiposConcurso");

            migrationBuilder.DropColumn(
                name: "NivelAsignado",
                table: "ElementosEvaluados");

            migrationBuilder.DropColumn(
                name: "Tematizado",
                table: "ElementosEvaluados");

            migrationBuilder.DropColumn(
                name: "BonusPorElementoTematizado",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.DropColumn(
                name: "ExigirMinimoUnAnioFallecimiento",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.AlterColumn<int>(
                name: "MaestroEncargadoId",
                table: "EquiposConcurso",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
