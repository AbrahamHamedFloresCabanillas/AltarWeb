using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class InvitacionesEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitacionesEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipoId = table.Column<int>(type: "int", nullable: false),
                    RegistranteId = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitacionesEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitacionesEquipo_EquiposConcurso_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "EquiposConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvitacionesEquipo_Registrantes_RegistranteId",
                        column: x => x.RegistranteId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesEquipo_EquipoId_RegistranteId",
                table: "InvitacionesEquipo",
                columns: new[] { "EquipoId", "RegistranteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesEquipo_RegistranteId_Periodo",
                table: "InvitacionesEquipo",
                columns: new[] { "RegistranteId", "Periodo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitacionesEquipo");
        }
    }
}
