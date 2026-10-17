using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracionPeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesPeriodo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Periodo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaLimiteInscripcion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaLimiteRequisitos = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecorridoPdf = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PesoObjetivoCultural = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoEsenciaPersonalidad = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoValoracionGeneral = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoDistribucionNiveles = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoNarrador = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ValorSatisfaccionNoPresente = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ValorSatisfaccionPoco = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ValorSatisfaccionSatisfactorio = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ValorSatisfaccionMuySatisfactorio = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoElementoRitual = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoElementoDecorativo = table.Column<decimal>(type: "decimal(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesPeriodo", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionesPeriodo",
                columns: new[] { "Id", "FechaLimiteInscripcion", "FechaLimiteRequisitos", "Periodo", "PesoDistribucionNiveles", "PesoElementoDecorativo", "PesoElementoRitual", "PesoEsenciaPersonalidad", "PesoNarrador", "PesoObjetivoCultural", "PesoValoracionGeneral", "RecorridoPdf", "ValorSatisfaccionMuySatisfactorio", "ValorSatisfaccionNoPresente", "ValorSatisfaccionPoco", "ValorSatisfaccionSatisfactorio" },
                values: new object[] { 1, null, null, "2026-1", 0.10m, 0.5m, 1.0m, 0.30m, 0.10m, 0.30m, 0.20m, null, 1.0m, 0.0m, 0.5m, 0.75m });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPeriodo_Periodo",
                table: "ConfiguracionesPeriodo",
                column: "Periodo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesPeriodo");
        }
    }
}
