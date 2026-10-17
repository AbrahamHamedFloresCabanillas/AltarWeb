using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregadoCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jueces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Usuario = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jueces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Evaluaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JuezId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NombreEquipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NombreDifunto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Niveles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoAltar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hobbies = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotaTradicion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NotaPersonalizacion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NotaEstetica = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NotaFinal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChkFoto = table.Column<bool>(type: "bit", nullable: false),
                    ChkVelas = table.Column<bool>(type: "bit", nullable: false),
                    ChkFlor = table.Column<bool>(type: "bit", nullable: false),
                    ChkPapel = table.Column<bool>(type: "bit", nullable: false),
                    ChkPan = table.Column<bool>(type: "bit", nullable: false),
                    ChkAgua = table.Column<bool>(type: "bit", nullable: false),
                    ChkSal = table.Column<bool>(type: "bit", nullable: false),
                    ChkIncienso = table.Column<bool>(type: "bit", nullable: false),
                    ChkCalaveritas = table.Column<bool>(type: "bit", nullable: false),
                    ChkObjetos = table.Column<bool>(type: "bit", nullable: false),
                    BonusTematicos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evaluaciones_Jueces_JuezId",
                        column: x => x.JuezId,
                        principalTable: "Jueces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Integrantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluacionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Matricula = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Integrantes_Evaluaciones_EvaluacionId",
                        column: x => x.EvaluacionId,
                        principalTable: "Evaluaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_JuezId",
                table: "Evaluaciones",
                column: "JuezId");

            migrationBuilder.CreateIndex(
                name: "IX_Integrantes_EvaluacionId",
                table: "Integrantes",
                column: "EvaluacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Integrantes");

            migrationBuilder.DropTable(
                name: "Evaluaciones");

            migrationBuilder.DropTable(
                name: "Jueces");
        }
    }
}
