using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCodigoPublicoAEquipos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Columna nullable para poder rellenar los equipos existentes antes de exigir NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "CodigoPublico",
                table: "EquiposConcurso",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContadoresCodigoPublico",
                columns: table => new
                {
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ultimo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContadoresCodigoPublico", x => x.Periodo);
                });

            // 2) Equipos existentes (activos e inactivos): consecutivo por periodo en orden de Id, el mismo
            //    formato que CodigoPublicoEquipo.Formatear (D4, crece si pasa de 9999).
            migrationBuilder.Sql(@"
WITH numerados AS (
    SELECT Id, Periodo, ROW_NUMBER() OVER (PARTITION BY Periodo ORDER BY Id) AS n
    FROM EquiposConcurso
)
UPDATE e
SET CodigoPublico = 'ALT-' + numerados.Periodo + '-' +
    CASE WHEN numerados.n < 10000 THEN RIGHT('0000' + CAST(numerados.n AS varchar(10)), 4)
         ELSE CAST(numerados.n AS varchar(10)) END
FROM EquiposConcurso e
JOIN numerados ON numerados.Id = e.Id;");

            // 3) Los contadores continuan desde el ultimo consecutivo asignado en cada periodo.
            migrationBuilder.Sql(@"
INSERT INTO ContadoresCodigoPublico (Periodo, Ultimo)
SELECT Periodo, COUNT(*) FROM EquiposConcurso GROUP BY Periodo;");

            // 4) Ya todos tienen codigo: NOT NULL + indice unico.
            migrationBuilder.AlterColumn<string>(
                name: "CodigoPublico",
                table: "EquiposConcurso",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquiposConcurso_CodigoPublico",
                table: "EquiposConcurso",
                column: "CodigoPublico",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContadoresCodigoPublico");

            migrationBuilder.DropIndex(
                name: "IX_EquiposConcurso_CodigoPublico",
                table: "EquiposConcurso");

            migrationBuilder.DropColumn(
                name: "CodigoPublico",
                table: "EquiposConcurso");
        }
    }
}
