using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistranteYEquipoNuevos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Registrantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Identificador = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CorreoInstitucional = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CatalogoGeneroId = table.Column<int>(type: "int", nullable: true),
                    CatalogoCarreraId = table.Column<int>(type: "int", nullable: true),
                    AutodescripcionCultural = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrantes_CatalogoCarreras_CatalogoCarreraId",
                        column: x => x.CatalogoCarreraId,
                        principalTable: "CatalogoCarreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Registrantes_CatalogoGeneros_CatalogoGeneroId",
                        column: x => x.CatalogoGeneroId,
                        principalTable: "CatalogoGeneros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EquiposConcurso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreAltar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CarreraId = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreadoPorRegistranteId = table.Column<int>(type: "int", nullable: true),
                    MaestroEncargadoId = table.Column<int>(type: "int", nullable: false),
                    UbicacionAltar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquiposConcurso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquiposConcurso_CatalogoCarreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "CatalogoCarreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquiposConcurso_Registrantes_CreadoPorRegistranteId",
                        column: x => x.CreadoPorRegistranteId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EquiposConcurso_Registrantes_MaestroEncargadoId",
                        column: x => x.MaestroEncargadoId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Difuntos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipoId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaDefuncion = table.Column<DateOnly>(type: "date", nullable: false),
                    SemblanzaHobbiesTematica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TipoAltar = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Difuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Difuntos_EquiposConcurso_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "EquiposConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistranteEquipos",
                columns: table => new
                {
                    RegistranteId = table.Column<int>(type: "int", nullable: false),
                    EquipoId = table.Column<int>(type: "int", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistranteEquipos", x => new { x.RegistranteId, x.EquipoId });
                    table.ForeignKey(
                        name: "FK_RegistranteEquipos_EquiposConcurso_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "EquiposConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistranteEquipos_Registrantes_RegistranteId",
                        column: x => x.RegistranteId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Difuntos_EquipoId",
                table: "Difuntos",
                column: "EquipoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquiposConcurso_CarreraId",
                table: "EquiposConcurso",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_EquiposConcurso_CreadoPorRegistranteId",
                table: "EquiposConcurso",
                column: "CreadoPorRegistranteId");

            migrationBuilder.CreateIndex(
                name: "IX_EquiposConcurso_MaestroEncargadoId",
                table: "EquiposConcurso",
                column: "MaestroEncargadoId");

            migrationBuilder.CreateIndex(
                name: "IX_EquiposConcurso_Nombre_Periodo",
                table: "EquiposConcurso",
                columns: new[] { "Nombre", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistranteEquipos_EquipoId",
                table: "RegistranteEquipos",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_CatalogoCarreraId",
                table: "Registrantes",
                column: "CatalogoCarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_CatalogoGeneroId",
                table: "Registrantes",
                column: "CatalogoGeneroId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_CorreoInstitucional",
                table: "Registrantes",
                column: "CorreoInstitucional",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_Identificador",
                table: "Registrantes",
                column: "Identificador",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Difuntos");

            migrationBuilder.DropTable(
                name: "RegistranteEquipos");

            migrationBuilder.DropTable(
                name: "EquiposConcurso");

            migrationBuilder.DropTable(
                name: "Registrantes");
        }
    }
}
