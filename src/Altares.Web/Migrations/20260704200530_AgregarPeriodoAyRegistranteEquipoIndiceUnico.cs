using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPeriodoAyRegistranteEquipoIndiceUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Periodo",
                table: "RegistranteEquipos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            // Backfill: las filas existentes heredan el Periodo de su Equipo (denormalizado desde ahora
            // en adelante). Necesario antes de crear el indice unico para no dejar todas las filas
            // existentes con Periodo="" (violaria la unicidad si un registrante ya tuviera mas de una).
            migrationBuilder.Sql(
                "UPDATE re SET re.Periodo = e.Periodo " +
                "FROM RegistranteEquipos re INNER JOIN EquiposConcurso e ON re.EquipoId = e.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_RegistranteEquipos_RegistranteId_Periodo",
                table: "RegistranteEquipos",
                columns: new[] { "RegistranteId", "Periodo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegistranteEquipos_RegistranteId_Periodo",
                table: "RegistranteEquipos");

            migrationBuilder.DropColumn(
                name: "Periodo",
                table: "RegistranteEquipos");
        }
    }
}
