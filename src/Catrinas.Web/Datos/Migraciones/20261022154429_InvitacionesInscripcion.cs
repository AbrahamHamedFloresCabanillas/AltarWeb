using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catrinas.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class InvitacionesInscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitacionesInscripcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InscripcionId = table.Column<int>(type: "int", nullable: false),
                    RegistranteId = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitacionesInscripcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitacionesInscripcion_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvitacionesInscripcion_Registrantes_RegistranteId",
                        column: x => x.RegistranteId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesInscripcion_InscripcionId_RegistranteId",
                table: "InvitacionesInscripcion",
                columns: new[] { "InscripcionId", "RegistranteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesInscripcion_RegistranteId_Periodo",
                table: "InvitacionesInscripcion",
                columns: new[] { "RegistranteId", "Periodo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitacionesInscripcion");
        }
    }
}
