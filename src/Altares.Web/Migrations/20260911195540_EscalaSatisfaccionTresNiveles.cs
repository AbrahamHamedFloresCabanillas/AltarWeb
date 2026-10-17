using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <summary>
    /// La escala de satisfaccion por elemento pasa de 4 niveles (No presente / Poco / Satisfactorio /
    /// Muy satisfactorio) a 3 (No lo tiene / Regular / Bueno). Cambio pedido el 2026-09-11.
    ///
    /// Columnas de ConfiguracionesPeriodo: se renombran conservando el valor de cada periodo
    /// (NoPresente->NoLoTiene, Poco->Regular, MuySatisfactorio->Bueno) y se elimina Satisfactorio.
    /// Datos de ElementosEvaluados (Satisfaccion se guarda como texto): Satisfactorio y
    /// MuySatisfactorio se colapsan en Bueno; Poco pasa a Regular; NoPresente a NoLoTiene.
    /// </summary>
    public partial class EscalaSatisfaccionTresNiveles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ValorSatisfaccionNoPresente",
                table: "ConfiguracionesPeriodo",
                newName: "ValorSatisfaccionNoLoTiene");

            migrationBuilder.RenameColumn(
                name: "ValorSatisfaccionPoco",
                table: "ConfiguracionesPeriodo",
                newName: "ValorSatisfaccionRegular");

            migrationBuilder.RenameColumn(
                name: "ValorSatisfaccionMuySatisfactorio",
                table: "ConfiguracionesPeriodo",
                newName: "ValorSatisfaccionBueno");

            migrationBuilder.DropColumn(
                name: "ValorSatisfaccionSatisfactorio",
                table: "ConfiguracionesPeriodo");

            migrationBuilder.Sql(@"
UPDATE ElementosEvaluados SET Satisfaccion = N'NoLoTiene' WHERE Satisfaccion = N'NoPresente';
UPDATE ElementosEvaluados SET Satisfaccion = N'Regular'   WHERE Satisfaccion = N'Poco';
UPDATE ElementosEvaluados SET Satisfaccion = N'Bueno'     WHERE Satisfaccion IN (N'Satisfactorio', N'MuySatisfactorio');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorSatisfaccionSatisfactorio",
                table: "ConfiguracionesPeriodo",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0.75m);

            migrationBuilder.RenameColumn(
                name: "ValorSatisfaccionBueno",
                table: "ConfiguracionesPeriodo",
                newName: "ValorSatisfaccionMuySatisfactorio");

            migrationBuilder.RenameColumn(
                name: "ValorSatisfaccionRegular",
                table: "ConfiguracionesPeriodo",
                newName: "ValorSatisfaccionPoco");

            migrationBuilder.RenameColumn(
                name: "ValorSatisfaccionNoLoTiene",
                table: "ConfiguracionesPeriodo",
                newName: "ValorSatisfaccionNoPresente");

            // Satisfactorio y MuySatisfactorio se colapsaron en Bueno; al revertir no se puede
            // distinguirlos, asi que todo Bueno vuelve como MuySatisfactorio.
            migrationBuilder.Sql(@"
UPDATE ElementosEvaluados SET Satisfaccion = N'NoPresente'       WHERE Satisfaccion = N'NoLoTiene';
UPDATE ElementosEvaluados SET Satisfaccion = N'Poco'             WHERE Satisfaccion = N'Regular';
UPDATE ElementosEvaluados SET Satisfaccion = N'MuySatisfactorio' WHERE Satisfaccion = N'Bueno';
");
        }
    }
}
