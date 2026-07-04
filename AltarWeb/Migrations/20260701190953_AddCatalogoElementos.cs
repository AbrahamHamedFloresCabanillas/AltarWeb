using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoElementos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Elementos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NivelSugerido = table.Column<int>(type: "int", nullable: true),
                    Significado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Colocacion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elementos", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Elementos",
                columns: new[] { "Id", "Activo", "Categoria", "Colocacion", "NivelSugerido", "Nombre", "Orden", "Significado" },
                values: new object[,]
                {
                    { 1, true, "RitualObligatorio", "En un vaso de cristal.", null, "El Agua", 1, "Fuente de la vida; mitiga la sed de las almas tras su viaje y simboliza la pureza del alma." },
                    { 2, true, "RitualObligatorio", "Jarra o jofaina acompañada de jabón pequeño y toalla/pañuelo limpio, para que el difunto se refresque al llegar.", null, "El Aguamanil y kit de aseo", 2, "Símbolo de purificación y hospitalidad." },
                    { 3, true, "RitualObligatorio", "En un plato pequeño, a veces formando una cruz hacia los cuatro puntos cardinales; suele acompañar al vaso de agua.", null, "La Sal", 3, "Principal elemento de purificación; limpia y protege el alma para que no se corrompa en su viaje; equilibrio espiritual." },
                    { 4, true, "RitualObligatorio", "Formando una cruz (cuatro puntos cardinales) y alrededor del camino y del altar; veladoras de vaso o cirios según la región.", null, "Velas y Veladoras", 4, "Fuego, luz, fe y esperanza; guían a las almas hacia el altar y de regreso." },
                    { 5, true, "RitualObligatorio", "Tradicionalmente en el penúltimo nivel o cerca de las imágenes.", null, "Incienso y Copal", 5, "El humo limpia las malas energías, purifica el ambiente y guía olfativamente a las almas. El copal es prehispánico (purificación y conexión espiritual); el incienso se asocia a la oración." },
                    { 6, true, "RitualObligatorio", "Senderos de pétalos hacia el altar, en jarrones/coronas y en arcos de bienvenida.", null, "Flor de Cempasúchil", 6, "Elemento principal; su color naranja representa el sol y su aroma guía a las almas a casa." },
                    { 7, true, "RitualObligatorio", "En la parte superior del altar para que el difunto reconozca su hogar.", null, "El Retrato del Difunto", 7, "Corazón de la ofrenda; sugiere el ánima que visitará a la familia." },
                    { 8, true, "RitualObligatorio", "Decorativas sobre el altar, coloridas con glaseado real.", null, "Calaveras de Azúcar", 8, "Representan la muerte, la vida efímera y el alma del difunto; evolución del tzompantli. Suelen llevar el nombre del ser querido en la frente." },
                    { 9, true, "RitualObligatorio", "Tequila, mezcal, cerveza o bebidas artesanales que disfrutaba en vida.", null, "El Licor (\"el trago\")", 9, "Para que el difunto recuerde los momentos de alegría que vivió." },
                    { 10, true, "RitualObligatorio", "Cruz grande de ceniza en el altar.", null, "Cruz de Ceniza", 10, "Expiación y purificación; ayuda al alma a expiar culpas y salir del purgatorio para visitar a los suyos." },
                    { 11, true, "RitualObligatorio", "Colgado sobre el altar y el espacio.", null, "Papel Picado", 11, "Elemento aire y fragilidad de la vida; al moverse con la brisa indica que las almas han llegado. Cada color tiene significado (naranja=sol/vida; morado=luto; blanco=pureza/niños; negro=inframundo; rosa=celebración; rojo=vida/sacrificio; azul=fallecidos por agua)." },
                    { 12, true, "RitualObligatorio", "Puede ser cualquier árbol/rama.", null, "La Vara (árbol)", 12, "Herramienta espiritual para que el difunto se defienda de malos espíritus y supere obstáculos en su viaje; considerado elemento de vida." },
                    { 13, true, "RitualObligatorio", "Como base/mantel; puede adornarse con flores.", null, "El Petate", 13, "Cama tejida de palma para que las ánimas descansen tras su travesía; también funciona como mantel/base de la ofrenda y une el mundo terrenal con el espiritual." },
                    { 14, true, "RitualObligatorio", "Prendas, objetos de uso cotidiano, artículos de pasatiempos y, para niños, juguetes.", null, "Objetos Personales", 14, "Conectan el alma con su identidad y lo que apreciaba en vida." },
                    { 15, true, "RitualObligatorio", "Sobre la ofrenda.", null, "Pan de Muerto", 15, "El más emblemático; ciclo de vida y muerte, fraternidad y ofrecimiento de alimento. La forma circular=eternidad; la esfera superior=cráneo/alma; las canillas=huesos y lágrimas (puntos cardinales); azúcar/ajonjolí=dulzura de la vida." },
                    { 16, true, "RitualObligatorio", "Sobre el petate/mantel.", null, "Comida y Bebida", 16, "Platillos, bebidas y dulces favoritos del difunto para deleitarlo en su visita." },
                    { 17, true, "RitualObligatorio", "En los niveles superiores junto a las imágenes.", null, "Objetos Religiosos o Místicos", 17, "Si el difunto era devoto, se incluyen rosarios, crucifijos, figuras de santos o amuletos." },
                    { 18, true, "RitualObligatorio", "En el nivel superior, mirando al frente, junto a las fotografías; de madera, resina o metal.", null, "Crucifijo", 18, "Simboliza la fe y sirve para que el ánima expíe sus culpas pendientes." },
                    { 19, true, "RitualObligatorio", "En la cúspide/último nivel o al frente del altar; tradicionalmente de carrizo, palma o madera flexible, con cruz de palma al centro, adornado con cempasúchil (frutas opcionales).", null, "El Arco", 19, "Puerta/umbral que une el mundo de los vivos con el más allá y da la bienvenida a las almas." },
                    { 20, true, "RitualObligatorio", "Camino de pétalos de cempasúchil (opcional sobre base de aserrín) desde el último escalón hasta el arco de bienvenida, acompañado de veladoras encendidas a los lados.", null, "El Camino", 20, "Sendero que guía a las almas desde el más allá hasta el altar y de regreso." },
                    { 21, true, "Decorativo", "Al final, como parte de la decoración general.", null, "Vasijas de Metal y de Barro", 21, "Elementos de decoración del altar." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Elementos_Nombre",
                table: "Elementos",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Elementos");
        }
    }
}
