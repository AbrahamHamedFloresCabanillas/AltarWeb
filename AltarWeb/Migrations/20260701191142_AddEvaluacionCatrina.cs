using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluacionCatrina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluacionesCatrina",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluacionId = table.Column<int>(type: "int", nullable: false),
                    SombreroTocado = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Guantes = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Vestimenta = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Zapatos = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Collar = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Maquillaje = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    NotaCatrina = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    LugarCatrina = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluacionesCatrina", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluacionesCatrina_EvaluacionesConcurso_EvaluacionId",
                        column: x => x.EvaluacionId,
                        principalTable: "EvaluacionesConcurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluacionesCatrina_EvaluacionId",
                table: "EvaluacionesCatrina",
                column: "EvaluacionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluacionesCatrina");
        }
    }
}
