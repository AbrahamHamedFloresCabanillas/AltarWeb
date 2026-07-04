using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogosGeneroYCarrera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogoCarreras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoCarreras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogoGeneros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoGeneros", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CatalogoCarreras",
                columns: new[] { "Id", "Activo", "Nombre", "Orden" },
                values: new object[,]
                {
                    { 1, true, "Lic. en Sistemas Computacionales", 1 },
                    { 2, true, "Bioingeniero", 2 },
                    { 3, true, "Ing. Aeroespacial", 3 },
                    { 4, true, "Ing. Civil", 4 },
                    { 5, true, "Ing. en Computación", 5 },
                    { 6, true, "Ing. en Electrónica", 6 },
                    { 7, true, "Ing. Eléctrico", 7 },
                    { 8, true, "Ing. en Energías Renovables", 8 },
                    { 9, true, "Ing. Industrial", 9 },
                    { 10, true, "Ing. Mecánico", 10 },
                    { 11, true, "Ing. en Mecatrónica", 11 },
                    { 12, true, "Ing. en Semiconductores y Microelectrónica", 12 },
                    { 13, true, "Ing. de Datos e Inteligencia Artificial", 13 }
                });

            migrationBuilder.InsertData(
                table: "CatalogoGeneros",
                columns: new[] { "Id", "Activo", "Nombre", "Orden" },
                values: new object[,]
                {
                    { 1, true, "Masculino", 1 },
                    { 2, true, "Femenino", 2 },
                    { 3, true, "No binario", 3 },
                    { 4, true, "Prefiero no especificar", 4 },
                    { 5, true, "Otro", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoCarreras_Nombre",
                table: "CatalogoCarreras",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoGeneros_Nombre",
                table: "CatalogoGeneros",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogoCarreras");

            migrationBuilder.DropTable(
                name: "CatalogoGeneros");
        }
    }
}
