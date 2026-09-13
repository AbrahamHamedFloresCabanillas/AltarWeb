using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <summary>
    /// Validacion administrativa de la Ficha de Registro (2026-09-11): el administrador puede marcar
    /// la ficha de un equipo como revisada para que deje de listarse como pendiente.
    ///
    /// Ambas columnas son nullable y su default es NULL = "sin validar", que es el estado correcto
    /// para todos los equipos existentes: no hace falta migrar datos ni tocar el seed.
    /// FichaValidadaPorJuezId es un escalar a proposito (sin FK ni navegacion): solo registra quien
    /// hizo la accion para trazabilidad, y no debe impedir dar de baja a un juez.
    /// </summary>
    public partial class ValidacionAdminDeFichaDeRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FichaValidadaPorAdminEn",
                table: "EquiposConcurso",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FichaValidadaPorJuezId",
                table: "EquiposConcurso",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FichaValidadaPorAdminEn",
                table: "EquiposConcurso");

            migrationBuilder.DropColumn(
                name: "FichaValidadaPorJuezId",
                table: "EquiposConcurso");
        }
    }
}
