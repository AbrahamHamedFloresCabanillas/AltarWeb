using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class SprintAlumnosEquiposConstancias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquipoId",
                table: "Evaluaciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NotaPersonalizacionFinal",
                table: "Evaluaciones",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NotaTradicionFinal",
                table: "Evaluaciones",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotNombreEquipo",
                table: "Evaluaciones",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Alumnos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreCompleto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Matricula = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CorreoElectronico = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProveedorAuth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodoRegistro = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alumnos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreEquipo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PeriodoAcademico = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreadoPorAlumnoId = table.Column<int>(type: "int", nullable: true),
                    SnapshotNombreCreador = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipos_Alumnos_CreadoPorAlumnoId",
                        column: x => x.CreadoPorAlumnoId,
                        principalTable: "Alumnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AlumnoEquipos",
                columns: table => new
                {
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    EquipoId = table.Column<int>(type: "int", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EsCreador = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlumnoEquipos", x => new { x.AlumnoId, x.EquipoId });
                    table.ForeignKey(
                        name: "FK_AlumnoEquipos_Alumnos_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlumnoEquipos_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_EquipoId",
                table: "Evaluaciones",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlumnoEquipos_EquipoId",
                table: "AlumnoEquipos",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_Alumnos_CorreoElectronico",
                table: "Alumnos",
                column: "CorreoElectronico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alumnos_Matricula",
                table: "Alumnos",
                column: "Matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_CreadoPorAlumnoId",
                table: "Equipos",
                column: "CreadoPorAlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_NombreEquipo_PeriodoAcademico",
                table: "Equipos",
                columns: new[] { "NombreEquipo", "PeriodoAcademico" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Evaluaciones_Equipos_EquipoId",
                table: "Evaluaciones",
                column: "EquipoId",
                principalTable: "Equipos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evaluaciones_Equipos_EquipoId",
                table: "Evaluaciones");

            migrationBuilder.DropTable(
                name: "AlumnoEquipos");

            migrationBuilder.DropTable(
                name: "Equipos");

            migrationBuilder.DropTable(
                name: "Alumnos");

            migrationBuilder.DropIndex(
                name: "IX_Evaluaciones_EquipoId",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "EquipoId",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "NotaPersonalizacionFinal",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "NotaTradicionFinal",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "SnapshotNombreEquipo",
                table: "Evaluaciones");
        }
    }
}
