using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catrinas.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class AceptacionAvisosLegales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvisoPrivacidadAceptadoEn",
                table: "Registrantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvisoPrivacidadVersion",
                table: "Registrantes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentimientoSensiblesEn",
                table: "Registrantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvisoPrivacidadAceptadoEn",
                table: "Jueces",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvisoPrivacidadVersion",
                table: "Jueces",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvisoPrivacidadAceptadoEn",
                table: "Registrantes");

            migrationBuilder.DropColumn(
                name: "AvisoPrivacidadVersion",
                table: "Registrantes");

            migrationBuilder.DropColumn(
                name: "ConsentimientoSensiblesEn",
                table: "Registrantes");

            migrationBuilder.DropColumn(
                name: "AvisoPrivacidadAceptadoEn",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "AvisoPrivacidadVersion",
                table: "Jueces");
        }
    }
}
