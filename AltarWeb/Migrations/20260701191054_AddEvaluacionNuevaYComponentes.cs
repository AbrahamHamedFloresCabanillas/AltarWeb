using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluacionNuevaYComponentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluacionesConcurso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipoId = table.Column<int>(type: "int", nullable: false),
                    JuezId = table.Column<int>(type: "int", nullable: true),
                    Periodo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Niveles = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PuntajeElementos = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DistribucionNiveles = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Narrador = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TematicaHobbies = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ObjetivoCultural = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EsenciaPersonalidad = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ValoracionGeneral = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IncluyeCatrina = table.Column<bool>(type: "bit", nullable: false),
                    NotaFinal = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Lugar = table.Column<int>(type: "int", nullable: true),
                    SnapshotNombreEquipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotNombreAltar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotDifuntoNombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotDifuntoFechaDefuncion = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluacionesConcurso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluacionesConcurso_EquiposConcurso_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "EquiposConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluacionesConcurso_Jueces_JuezId",
                        column: x => x.JuezId,
                        principalTable: "Jueces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ElementosEvaluados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluacionId = table.Column<int>(type: "int", nullable: false),
                    ElementoId = table.Column<int>(type: "int", nullable: false),
                    Satisfaccion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementosEvaluados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementosEvaluados_Elementos_ElementoId",
                        column: x => x.ElementoId,
                        principalTable: "Elementos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElementosEvaluados_EvaluacionesConcurso_EvaluacionId",
                        column: x => x.EvaluacionId,
                        principalTable: "EvaluacionesConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluacionIntegrantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluacionId = table.Column<int>(type: "int", nullable: false),
                    RegistranteId = table.Column<int>(type: "int", nullable: true),
                    NombreCompleto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Identificador = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluacionIntegrantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluacionIntegrantes_EvaluacionesConcurso_EvaluacionId",
                        column: x => x.EvaluacionId,
                        principalTable: "EvaluacionesConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluacionIntegrantes_Registrantes_RegistranteId",
                        column: x => x.RegistranteId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElementosEvaluados_ElementoId",
                table: "ElementosEvaluados",
                column: "ElementoId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosEvaluados_EvaluacionId_ElementoId",
                table: "ElementosEvaluados",
                columns: new[] { "EvaluacionId", "ElementoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionesConcurso_EquipoId",
                table: "EvaluacionesConcurso",
                column: "EquipoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionesConcurso_JuezId",
                table: "EvaluacionesConcurso",
                column: "JuezId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionIntegrantes_EvaluacionId",
                table: "EvaluacionIntegrantes",
                column: "EvaluacionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionIntegrantes_RegistranteId",
                table: "EvaluacionIntegrantes",
                column: "RegistranteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElementosEvaluados");

            migrationBuilder.DropTable(
                name: "EvaluacionIntegrantes");

            migrationBuilder.DropTable(
                name: "EvaluacionesConcurso");
        }
    }
}
