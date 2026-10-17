using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarElementosFaltantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Elementos",
                columns: new[] { "Id", "Activo", "Categoria", "Colocacion", "NivelSugerido3", "NivelSugerido7", "Nombre", "Orden", "Significado" },
                values: new object[,]
                {
                    { 22, true, "RitualObligatorio", "Imagen o estampa religiosa junto a las demás imágenes santas, en los niveles superiores.", 3, 7, "Estampa del Alma del Purgatorio", 22, "Recuerda el propósito religioso original de la ofrenda: orar por el alma del difunto en su tránsito por el purgatorio." },
                    { 23, true, "RitualObligatorio", "Sobre el altar o junto al camino de pétalos de cempasúchil.", 1, 2, "Monedas", 23, "Representan el pago del paso que el alma debe cubrir en su travesía de regreso al mundo de los vivos." },
                    { 24, true, "RitualObligatorio", "Figura del perro colocada cerca del camino o en la base del altar.", 1, 1, "Figura de Perro Negro o Xoloitzcuintli", 24, "El xoloitzcuintli guía y ayuda al alma a cruzar el río hacia el Mictlán; simboliza lealtad y protección durante el viaje." }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 24);
        }
    }
}
