using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AltarWeb.Models.Registro.Configurations
{
    public class ElementoConfiguration : IEntityTypeConfiguration<Elemento>
    {
        public void Configure(EntityTypeBuilder<Elemento> builder)
        {
            builder.ToTable("Elementos");

            builder.Property(e => e.Categoria).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(e => e.Nombre).IsUnique();

            // Catalogo maestro, seccion 8 de vision.md. NivelSugerido3/7: propuesta confirmada por
            // el comite (numeracion 1=base/piso, N=cuspide, tal como usa el propio vision.md para
            // "El Arco" -> "cuspide/ultimo nivel"). El esquema de 3 niveles es una compresion
            // sistematica del de 7 (1-2->1, 3-5->2, 6-7->3).
            builder.HasData(
                new Elemento
                {
                    Id = 1,
                    Nombre = "El Agua",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 1,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 2,
                    Significado = "Fuente de la vida; mitiga la sed de las almas tras su viaje y simboliza la pureza del alma.",
                    Colocacion = "En un vaso de cristal."
                },
                new Elemento
                {
                    Id = 2,
                    Nombre = "El Aguamanil y kit de aseo",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 2,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 2,
                    Significado = "Símbolo de purificación y hospitalidad.",
                    Colocacion = "Jarra o jofaina acompañada de jabón pequeño y toalla/pañuelo limpio, para que el difunto se refresque al llegar."
                },
                new Elemento
                {
                    Id = 3,
                    Nombre = "La Sal",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 3,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 2,
                    Significado = "Principal elemento de purificación; limpia y protege el alma para que no se corrompa en su viaje; equilibrio espiritual.",
                    Colocacion = "En un plato pequeño, a veces formando una cruz hacia los cuatro puntos cardinales; suele acompañar al vaso de agua."
                },
                new Elemento
                {
                    Id = 4,
                    Nombre = "Velas y Veladoras",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 4,
                    NivelSugerido3 = 2,
                    NivelSugerido7 = 4,
                    Significado = "Fuego, luz, fe y esperanza; guían a las almas hacia el altar y de regreso.",
                    Colocacion = "Formando una cruz (cuatro puntos cardinales) y alrededor del camino y del altar; veladoras de vaso o cirios según la región."
                },
                new Elemento
                {
                    Id = 5,
                    Nombre = "Incienso y Copal",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 5,
                    NivelSugerido3 = 3,
                    NivelSugerido7 = 6,
                    Significado = "El humo limpia las malas energías, purifica el ambiente y guía olfativamente a las almas. El copal es prehispánico (purificación y conexión espiritual); el incienso se asocia a la oración.",
                    Colocacion = "Tradicionalmente en el penúltimo nivel o cerca de las imágenes."
                },
                new Elemento
                {
                    Id = 6,
                    Nombre = "Flor de Cempasúchil",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 6,
                    NivelSugerido3 = 2,
                    NivelSugerido7 = 3,
                    Significado = "Elemento principal; su color naranja representa el sol y su aroma guía a las almas a casa.",
                    Colocacion = "Senderos de pétalos hacia el altar, en jarrones/coronas y en arcos de bienvenida."
                },
                new Elemento
                {
                    Id = 7,
                    Nombre = "El Retrato del Difunto",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 7,
                    NivelSugerido3 = 3,
                    NivelSugerido7 = 7,
                    Significado = "Corazón de la ofrenda; sugiere el ánima que visitará a la familia.",
                    Colocacion = "En la parte superior del altar para que el difunto reconozca su hogar."
                },
                new Elemento
                {
                    Id = 8,
                    Nombre = "Calaveras de Azúcar",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 8,
                    NivelSugerido3 = 2,
                    NivelSugerido7 = 3,
                    Significado = "Representan la muerte, la vida efímera y el alma del difunto; evolución del tzompantli. Suelen llevar el nombre del ser querido en la frente.",
                    Colocacion = "Decorativas sobre el altar, coloridas con glaseado real."
                },
                new Elemento
                {
                    Id = 9,
                    Nombre = "El Licor (\"el trago\")",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 9,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 2,
                    Significado = "Para que el difunto recuerde los momentos de alegría que vivió.",
                    Colocacion = "Tequila, mezcal, cerveza o bebidas artesanales que disfrutaba en vida."
                },
                new Elemento
                {
                    Id = 10,
                    Nombre = "Cruz de Ceniza",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 10,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 2,
                    Significado = "Expiación y purificación; ayuda al alma a expiar culpas y salir del purgatorio para visitar a los suyos.",
                    Colocacion = "Cruz grande de ceniza en el altar."
                },
                new Elemento
                {
                    Id = 11,
                    Nombre = "Papel Picado",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 11,
                    NivelSugerido3 = 3,
                    NivelSugerido7 = 6,
                    Significado = "Elemento aire y fragilidad de la vida; al moverse con la brisa indica que las almas han llegado. Cada color tiene significado (naranja=sol/vida; morado=luto; blanco=pureza/niños; negro=inframundo; rosa=celebración; rojo=vida/sacrificio; azul=fallecidos por agua).",
                    Colocacion = "Colgado sobre el altar y el espacio."
                },
                new Elemento
                {
                    Id = 12,
                    Nombre = "La Vara (árbol)",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 12,
                    NivelSugerido3 = 2,
                    NivelSugerido7 = 3,
                    Significado = "Herramienta espiritual para que el difunto se defienda de malos espíritus y supere obstáculos en su viaje; considerado elemento de vida.",
                    Colocacion = "Puede ser cualquier árbol/rama."
                },
                new Elemento
                {
                    Id = 13,
                    Nombre = "El Petate",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 13,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 1,
                    Significado = "Cama tejida de palma para que las ánimas descansen tras su travesía; también funciona como mantel/base de la ofrenda y une el mundo terrenal con el espiritual.",
                    Colocacion = "Como base/mantel; puede adornarse con flores."
                },
                new Elemento
                {
                    Id = 14,
                    Nombre = "Objetos Personales",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 14,
                    NivelSugerido3 = 2,
                    NivelSugerido7 = 3,
                    Significado = "Conectan el alma con su identidad y lo que apreciaba en vida.",
                    Colocacion = "Prendas, objetos de uso cotidiano, artículos de pasatiempos y, para niños, juguetes."
                },
                new Elemento
                {
                    Id = 15,
                    Nombre = "Pan de Muerto",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 15,
                    NivelSugerido3 = 2,
                    NivelSugerido7 = 4,
                    Significado = "El más emblemático; ciclo de vida y muerte, fraternidad y ofrecimiento de alimento. La forma circular=eternidad; la esfera superior=cráneo/alma; las canillas=huesos y lágrimas (puntos cardinales); azúcar/ajonjolí=dulzura de la vida.",
                    Colocacion = "Sobre la ofrenda."
                },
                new Elemento
                {
                    Id = 16,
                    Nombre = "Comida y Bebida",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 16,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 1,
                    Significado = "Platillos, bebidas y dulces favoritos del difunto para deleitarlo en su visita.",
                    Colocacion = "Sobre el petate/mantel."
                },
                new Elemento
                {
                    Id = 17,
                    Nombre = "Objetos Religiosos o Místicos",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 17,
                    NivelSugerido3 = 3,
                    NivelSugerido7 = 7,
                    Significado = "Si el difunto era devoto, se incluyen rosarios, crucifijos, figuras de santos o amuletos.",
                    Colocacion = "En los niveles superiores junto a las imágenes."
                },
                new Elemento
                {
                    Id = 18,
                    Nombre = "Crucifijo",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 18,
                    NivelSugerido3 = 3,
                    NivelSugerido7 = 7,
                    Significado = "Simboliza la fe y sirve para que el ánima expíe sus culpas pendientes.",
                    Colocacion = "En el nivel superior, mirando al frente, junto a las fotografías; de madera, resina o metal."
                },
                new Elemento
                {
                    Id = 19,
                    Nombre = "El Arco",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 19,
                    NivelSugerido3 = 3,
                    NivelSugerido7 = 7,
                    Significado = "Puerta/umbral que une el mundo de los vivos con el más allá y da la bienvenida a las almas.",
                    Colocacion = "En la cúspide/último nivel o al frente del altar; tradicionalmente de carrizo, palma o madera flexible, con cruz de palma al centro, adornado con cempasúchil (frutas opcionales)."
                },
                new Elemento
                {
                    Id = 20,
                    Nombre = "El Camino",
                    Categoria = CategoriaElemento.RitualObligatorio,
                    Orden = 20,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 1,
                    Significado = "Sendero que guía a las almas desde el más allá hasta el altar y de regreso.",
                    Colocacion = "Camino de pétalos de cempasúchil (opcional sobre base de aserrín) desde el último escalón hasta el arco de bienvenida, acompañado de veladoras encendidas a los lados."
                },
                new Elemento
                {
                    Id = 21,
                    Nombre = "Vasijas de Metal y de Barro",
                    Categoria = CategoriaElemento.Decorativo,
                    Orden = 21,
                    NivelSugerido3 = 1,
                    NivelSugerido7 = 1,
                    Significado = "Elementos de decoración del altar.",
                    Colocacion = "Al final, como parte de la decoración general."
                }
            );
        }
    }
}
