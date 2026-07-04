# AltarWeb — Visión y Especificación Funcional (v2.5)

> Documento base para la **re-implementación** del sistema de gestión y evaluación del
> **Concurso de Altares de Muertos de la Facultad de Ingeniería (FIM) de la UABC**,
> tras el cambio de requerimientos. Sustituye al README anterior como fuente de verdad
> del *qué* construir. Está redactado para guiar directamente a los agentes de desarrollo.
>
> **v2.5** especifica el **Reporte de Cierre de Periodo** (sección 12): lista completa
> de métricas a contabilizar (participación, distribución académica, resultados de
> evaluación, demografía) y una **regla obligatoria de umbral de agrupación (`N = 5`)**
> para proteger `Genero` y `AutodescripcionCultural` de des-anonimización en cortes
> reducidos. **Pendiente:** escala de satisfacción, regla de empates, y normalización de
> `AutodescripcionCultural` para conteo (sección 13).

---

## 1. Propósito y alcance

AltarWeb permite a los jueces del concurso **registrar evaluaciones de altares**, calcular
puntajes bajo los nuevos criterios culturales, gestionar el **registro de participantes**
(alumnos, maestros y administrativos), administrar **equipos por carrera y periodo**, y
generar y enviar **constancias oficiales en PDF** a todos los participantes, maestros
encargados y jueces.

El concurso se organiza **por carrera**: cada equipo presenta un altar asociado a una
carrera, y el juez recorre y califica los altares de esa carrera en su ubicación física.

---

## 2. Cambios respecto a la versión anterior (breaking changes)

Esta versión **no es retrocompatible** con el esquema actual. Cambios estructurales:

1. **Portal de Alumnos → Portal de Registro**: ahora registra alumnos, maestros y administrativos.
2. **Identificador unificado**: matrícula (alumno) / no. de empleado o matrícula de 5 dígitos (maestro/administrativo).
3. **Nuevos campos demográficos** del registrante: género, carrera, autodescripción cultural.
4. **Características del difunto**: se agrega año de fallecimiento (y semblanza/hobbies).
5. **Checklist completamente nuevo**: nuevo catálogo de ~21 elementos (sección 8) y **escala de satisfacción** por elemento en lugar de check binario + bonus temático.
6. **Evaluación organizada por nivel** del altar; los **niveles entran en la calificación**.
7. **Renombre de las tres grandes categorías** de calificación (sección 7.3).
8. **Evaluación del narrador** como componente calificable.
9. **Catrina** como **sección de evaluación separada y opcional**.
10. **Estados de evaluación**: `Preliminar` (editable) y `Final` (con confirmación).
11. **Constancias para todos los participantes** sin importar la nota, más constancia para **juez** y **maestro encargado**; las constancias muestran el **lugar/posición** del equipo.
12. **Fechas límite** configurables por el administrador (inscripción y requisitos completos).
13. **Recorrido en PDF** subido por el administrador para que los jueces lo consulten.
14. **Se retira el Nivel 2** (solo 3 o 7 niveles) y el **tipo de altar "Mascotas"**.
15. **Ficha de Registro del Altar** (sección 6.1): nuevo flujo formal que el organizador llena al registrar el equipo (responsable, teléfono, nombre del altar, difunto con fecha de defunción, programa educativo, lugar de exposición). Estos datos se precargan automáticamente al momento de la evaluación, igual que hoy ocurre con el nombre del difunto.

---

## 3. Actores y roles

### 3.1 Roles del sistema (autenticación interna jueces/admin)
- **Juez**: opera la evaluación. Inicia/cierra sesión, crea y edita evaluaciones (preliminar→final), consulta historial por periodo, ve cuántos equipos hay por carrera y cuáles ya fueron calificados, consulta el recorrido PDF y la ubicación de cada altar, y reenvía constancias.
- **Administrador**: todo lo del juez más la gestión de jueces, registrantes, equipos, configuración del periodo (fechas límite, recorrido PDF), y la reportería por periodo.

### 3.2 Tipos de registrante (Portal de Registro)
Son **participantes**, no usuarios del backoffice. Se distinguen por su identificador:

| Tipo | Identificador | Notas |
|------|---------------|-------|
| **Alumno** | Matrícula de **7 dígitos** | Crea y forma parte de equipos; tiene carrera. |
| **Maestro** | Matrícula de **5 dígitos** | Puede ser designado como maestro encargado de un equipo; recibe constancia. |
| **Administrativo** | Matrícula de **5 dígitos** | Mismo formato que Maestro; se distingue por el campo `Tipo`. Participa/registra; recibe constancia. |

Todos los registrantes usan correo institucional **`@uabc.edu.mx`** (registro local con
contraseña o Google OAuth restringido a ese dominio).

---

## 4. Modelo de dominio (entidades y campos)

> Persistencia con EF Core. Se conservan los modelos históricos (`Integrante`) solo para
> evaluaciones antiguas; el flujo nuevo usa `Registrante` y `RegistranteEquipo`.

### 4.1 Registrante (antes Alumno)
- `Id`
- `Tipo` *(Alumno | Maestro | Administrativo)*
- `NombreCompleto`
- `Identificador` — **dos formatos válidos, según tipo:**
  - **Alumno:** matrícula de **7 dígitos**.
  - **Maestro / Administrativo:** matrícula de **5 dígitos**.
  - Validar formato (longitud y solo dígitos) según `Tipo` al capturar.
- `CorreoInstitucional` *(`@uabc.edu.mx`)*
- `Telefono` *(campo de contacto — dato histórico de la Ficha de Registro en papel; obligatorio para el organizador del equipo, opcional para el resto de los integrantes)*
- `Genero` *(catálogo simple y editable desde el admin — ver Apéndice B)*
- `Carrera` *(catálogo FIM oficial, 13 programas — ver Apéndice C)*
- `AutodescripcionCultural` *(**campo de texto abierto**, no opción múltiple — el registrante describe libremente si se autoadscribe a pueblos originarios, afroamericanos, migrantes u otros grupos culturalmente diversos, tal como se planteó en el documento de requerimientos; puede dejarse en blanco)*
- `Activo` *(soft delete)*
- `CreadoEn`

### 4.2 Equipo
- `Id`
- `Nombre` *(nombre del grupo/equipo)*
- `NombreAltar` *(nombre propio del altar — distinto del nombre del equipo, p. ej. equipo "zaleto", altar "La mano peluda"; capturado en la Ficha de Registro)*
- `Carrera` *(carrera **del equipo/altar** — es la carrera a la que se presenta el altar; los integrantes pueden pertenecer a cualquier carrera, no necesariamente coincide con la `Carrera` individual del registrante)*
- `Periodo` *(`YYYY-1` / `YYYY-2`)*
- `CreadoPorRegistranteId` *(organizador del equipo / responsable de la Ficha de Registro)*
- `MaestroEncargadoId` *(FK a Registrante tipo Maestro — obligatorio; el alumno lo ingresa)*
- `UbicacionAltar` *("Lugar de exposición" — el organizador lo declara al llenar la Ficha de Registro; el administrador puede revisarlo/corregirlo antes de la fecha límite de requisitos)*
- `Activo`
- Relación **RegistranteEquipo** (integrantes, de cualquier carrera) con `Rol` *(Integrante | Narrador)*.

**Reglas de equipo**
- Un registrante **solo puede pertenecer a un equipo activo por periodo** (validar al unirse/crear).
- El organizador queda como integrante y no puede quitarse a sí mismo.
- Mientras el equipo **no tenga evaluación**, el organizador puede agregar/quitar integrantes.
- Tras evaluarse, la lista de integrantes queda **cerrada** (conserva historial y constancias).
- **Baja del organizador antes de la fecha límite**: debe asignar un nuevo organizador; si no, el equipo queda **pendiente** para que el administrador lo reasigne/modifique.
- Debe existir **un Narrador** designado entre los integrantes.

### 4.3 Difunto (datos del altar)
- `Nombre`
- `FechaDefuncion` *(fecha completa — reemplaza el campo `AnioFallecimiento` de la propuesta anterior; se captura día/mes/año como en la Ficha de Registro histórica)*
- `Semblanza/HobbiesTematica` *(texto: gustos, personalidad, temática del altar)*
- `TipoAltar` *(Tradicional | Niños)* — **se retira "Mascotas"**

### 4.4 Evaluación / Altar
- `Id`, `EquipoId`, `JuezId`, `Periodo`
- `Niveles` *(3 | 7)* — **se retira el nivel 2**
- `Estado` *(Preliminar | Final)*
- Componentes calificables (sección 7): puntaje de elementos, distribución por niveles, narrador, temática/hobbies, y las tres categorías renombradas.
- `IncluyeCatrina` *(bool)* + relación a **EvaluacionCatrina** (sección 7.5)
- `NotaFinal` *(calculada)*
- `Lugar` *(1°/2°/3°/—, calculado al cierre, **por la `Carrera` del equipo/altar**, sin importar la carrera de cada integrante)*
- `CreadoEn`, `ActualizadoEn`

> **Snapshot**: al evaluar, se guarda copia del nombre del equipo, nombre del altar,
> integrantes y datos del difunto (incluida la fecha de defunción) para preservar el
> historial aunque luego se desactiven o modifiquen registros.

### 4.5 Elemento (catálogo) y ElementoEvaluado
- **Elemento** (catálogo maestro, ver sección 8): `Id`, `Nombre`, `Categoria` *(Ritual/Obligatorio | Decorativo)*, `NivelSugerido`, `Significado`, `Colocacion` *(textos del mini-manual)*, `Orden`.
- **ElementoEvaluado** (por evaluación): `ElementoId`, `Satisfaccion` *(No presente | Poco | Satisfactorio | Muy satisfactorio)*.

### 4.6 ConfiguracionPeriodo
- `Periodo`
- `FechaLimiteInscripcion`
- `FechaLimiteRequisitos` *(para X fecha los equipos deben tener todos los requisitos)*
- `RecorridoPdf` *(archivo subido por el admin)*
- Pesos de calificación (sección 7.7) si se decide hacerlos configurables.

---

## 5. Portal de Registro (antes Portal de Alumnos)

- Ruta sugerida: `/Registro/Login` (sesión independiente del backoffice de jueces/admin).
- Registro local (nombre, identificador según tipo, correo institucional, contraseña) o **Google OAuth** restringido a `@uabc.edu.mx`. Si el correo no existe, completa identificador/tipo antes de entrar.
- **Dashboard** del participante: estado de su equipo, **estado de la Ficha de Registro del Altar** (completa/pendiente), integrantes, maestro encargado, ubicación del altar, y resultado de evaluación cuando exista.
- **Listado de elementos requeridos**: dentro del panel, se muestra a los participantes el catálogo de elementos obligatorios (desde el catálogo maestro) para que sepan qué preparar.
- Descarga de su constancia cuando esté disponible.

---

## 6. Gestión de equipos y Ficha de Registro del Altar

### 6.1 Ficha de Registro del Altar (nuevo flujo formal)
Reemplaza el registro mínimo de "nombre de equipo + integrantes" por una **ficha de
registro completa**, equivalente digital de la ficha en papel históricamente usada por
APFI/FIM, que el **organizador del equipo** llena una sola vez (editable hasta que el
equipo tenga una evaluación registrada, o hasta la `FechaLimiteRequisitos`). Los datos
capturados aquí son los que **se precargan automáticamente al momento de la evaluación**
(igual que ya ocurre hoy con el nombre del difunto):

| Campo en la ficha | Entidad / propiedad |
|---|---|
| Nombre completo del responsable | `Registrante.NombreCompleto` (organizador) |
| Matrícula o no. de empleado | `Registrante.Identificador` |
| Correo electrónico | `Registrante.CorreoInstitucional` |
| Teléfono | `Registrante.Telefono` |
| Nombre del grupo | `Equipo.Nombre` |
| Nombre del altar | `Equipo.NombreAltar` |
| Nombre del difunto al que se ofrece el altar | `Difunto.Nombre` |
| Fecha de defunción | `Difunto.FechaDefuncion` |
| Nombre de los participantes o colaboradores | `RegistranteEquipo` (vía flujo existente de integrantes; no se captura como texto libre, se vincula a registrantes reales) |
| Programa educativo o laboratorio | `Equipo.Carrera` |
| Lugar de exposición | `Equipo.UbicacionAltar` |
| Fecha de inscripción | `Equipo.CreadoEn` *(automática)* |

**Reglas de la ficha**
- La ficha debe quedar **completa** antes de la `FechaLimiteRequisitos` configurada por el
  administrador (sección 11); el sistema puede marcar equipos con ficha incompleta para
  seguimiento del admin.
- Mientras el equipo no tenga evaluación, el organizador puede seguir editando su Ficha de
  Registro (incluida `UbicacionAltar`); el administrador también puede editarla/corregirla
  desde `/Admin/EditarEquipo/{id}`.

### 6.2 Administración de equipos
- Los participantes crean/administran sus equipos y su Ficha de Registro asociada, dentro del periodo actual.
- El administrador gestiona equipos activos (`/Admin/Equipos`), históricos (`/Admin/EquiposHistorico`) y la edición de la ficha completa (nombre, altar, difunto, integrantes, maestro, ubicación) desde `/Admin/EditarEquipo/{id}`, así como el listado de registrantes (`/Admin/Registrantes`: alta, búsqueda, soft delete y reactivación).
- Validaciones: pertenencia única por periodo, narrador designado, maestro encargado asignado, ficha de registro completa antes de la fecha límite.

---

## 7. Proceso de evaluación

Acceso desde `Evaluacion/Crear`. El juez busca el equipo por carrera (búsqueda en tiempo
real). Al seleccionarlo, la vista precarga en **solo lectura** todos los datos capturados
en su **Ficha de Registro** (sección 6.1): integrantes, nombre del altar, nombre y fecha
de defunción del difunto, programa educativo/carrera, lugar de exposición, y datos de
contacto del responsable. El sistema valida que el equipo no tenga ya una evaluación
registrada. La interfaz se organiza por pestañas/secciones.

### 7.1 Estructura por niveles
- El altar es de **3 o 7 niveles**.
- El **checklist se presenta agrupado por nivel**: cada elemento tiene un `NivelSugerido` y se muestra en su nivel correspondiente; los elementos disponibles se van listando nivel por nivel según el número de niveles del altar.
- Los **niveles entran en la calificación**: el juez asigna una **Nota de Distribución por Niveles (0–10)** según si los elementos están colocados en los niveles correctos.

### 7.2 Checklist de elementos (escala de satisfacción)
Reemplaza el check binario + bonus temático. Cada elemento se califica con una **escala de satisfacción** que integra presencia + creatividad/criterio del juez:

| Opción | Valor (configurable) |
|--------|----------------------|
| No presente | `0.0` |
| Poco satisfactorio | `0.5` |
| Satisfactorio | `0.75` |
| Muy satisfactorio | `1.0` |

- Cada elemento muestra un ícono **(i)** que abre su **mini-manual** (significado + cómo/dónde se coloca) tomado del catálogo de la sección 8.
- Acción rápida **"Marcar todo"** (p. ej. todo como *Satisfactorio*) cuando el altar tiene todo, ajustable después.
- A medida que se califican elementos, se muestra el **acumulado de puntos** obtenidos.
- Los elementos **Decorativos** (p. ej. vasijas de metal y barro) van al final y pueden marcarse como opcionales sin penalizar igual que los rituales (peso configurable).

**Puntaje de Elementos (0–10):**
```
PuntajeElementos = ( Σ(satisfacción · peso) / Σ(peso_max) ) · 10
```

### 7.3 Categorías de calificación (renombradas)
Las tres grandes categorías cambian de nombre y de enfoque conceptual; cada una en escala **0–10**:

| Antes | Ahora | Pregunta guía |
|-------|-------|----------------|
| Tradición | **Objetivo Cultural** | ¿Logró su objetivo cultural el altar? |
| Personalización | **Esencia y Personalidad** | ¿Logró plasmar la esencia y personalidad de a quién va dirigido el altar? |
| Estética | **Valoración General** | (Objetivos generales) En términos generales, tanto en lo personal como en lo cultural, ¿qué calificación otorga? |

El sistema puede **sugerir** un valor inicial de *Objetivo Cultural* a partir del Puntaje de Elementos y la Distribución por Niveles, pero el juez confirma o ajusta.

### 7.4 Narrador y Temática/Hobbies
- **Narrador (0–10)**: se califica la explicación del equipo (el narrador presenta el altar). Incluye su propio (i) con la guía de qué evaluar para el juez.
- **Temática/Hobbies (0–10)**: qué tan bien refleja los gustos/personalidad del difunto y la creatividad del planteamiento. Se apoya en la semblanza capturada (4.3) y en los objetos personales.

### 7.5 Catrina (categoría de evaluación aparte)
Si el equipo decide hacer **Catrina**, se habilita una sección aparte (`IncluyeCatrina = true`).
**Confirmado: la Catrina es una categoría independiente** — no suma a `NotaFinal` del altar.
Se evalúa, rankea y premia por separado (p. ej. "Mejor Catrina" por carrera o general).
Se califican (escala 0–10 c/u, o satisfacción si se prefiere homologar con 7.2) los elementos:
- Sombrero y tocado
- Guantes
- Vestimenta
- Zapatos
- Collar
- Maquillaje

Se calcula una **Nota de Catrina** (promedio de sus rubros) y un **ranking propio de Catrina por carrera**, independiente del ranking del altar.

### 7.6 Estados: Preliminar / Final
- El juez puede **guardar como `Preliminar`** y volver a modificar las veces que necesite.
- El paso a **`Final`** requiere una confirmación explícita (*"¿Estás de acuerdo?"*) que revisa el resumen de todas las calificaciones antes de cerrar.
- Solo las evaluaciones `Final` cuentan para ranking, constancias y reportería.

### 7.7 Fórmulas de cálculo (pesos configurables desde el sistema)
**Componentes (todos 0–10):** Objetivo Cultural (`C`), Esencia y Personalidad (`P`),
Valoración General (`G`), Distribución por Niveles (`N`), Narrador (`R`).

**Ponderación confirmada como punto de partida** (suma = 100%), **editable desde el panel de
administración** (parte de `ConfiguracionPeriodo`, sin requerir despliegue de código para
cambiarla):
```
NotaFinal = C·30% + P·30% + G·20% + N·10% + R·10%
```

> Nota: el *Puntaje de Elementos* (7.2) alimenta como insumo/sugerencia a `C`, en lugar de
> sumar por separado, para no duplicar el peso de los elementos.

La **Nota de Catrina** es **siempre independiente** de esta fórmula (categoría propia, sección 7.5).

---

## 8. Catálogo de elementos del altar (mini-manual para el juez)

Contenido base del catálogo maestro. Cada ficha alimenta el modal **(i)** del checklist y
el listado de elementos requeridos del Portal de Registro. *(`R` = Ritual/Obligatorio,
`D` = Decorativo.)*

1. **El Agua** — `R`. *Significado:* fuente de la vida; mitiga la sed de las almas tras su viaje y simboliza la pureza del alma. *Colocación:* en un vaso de cristal.
2. **El Aguamanil y kit de aseo** — `R`. *Significado:* símbolo de purificación y hospitalidad. *Colocación:* jarra o jofaina acompañada de jabón pequeño y toalla/pañuelo limpio, para que el difunto se refresque al llegar.
3. **La Sal** — `R`. *Significado:* principal elemento de purificación; limpia y protege el alma para que no se corrompa en su viaje; equilibrio espiritual. *Colocación:* en un plato pequeño, a veces formando una cruz hacia los cuatro puntos cardinales; suele acompañar al vaso de agua.
4. **Velas y Veladoras** — `R`. *Significado:* fuego, luz, fe y esperanza; guían a las almas hacia el altar y de regreso. *Colocación:* formando una cruz (cuatro puntos cardinales) y alrededor del camino y del altar; veladoras de vaso o cirios según la región.
5. **Incienso y Copal** — `R`. *Significado:* el humo limpia las malas energías, purifica el ambiente y guía olfativamente a las almas. El copal es prehispánico (purificación y conexión espiritual); el incienso se asocia a la oración. *Colocación:* tradicionalmente en el penúltimo nivel o cerca de las imágenes.
6. **Flor de Cempasúchil** — `R`. *Significado:* elemento principal; su color naranja representa el sol y su aroma guía a las almas a casa. *Colocación:* senderos de pétalos hacia el altar, en jarrones/coronas y en arcos de bienvenida.
7. **El Retrato del Difunto** — `R`. *Significado:* corazón de la ofrenda; sugiere el ánima que visitará a la familia. *Colocación:* en la parte superior del altar para que el difunto reconozca su hogar.
8. **Calaveras de Azúcar** — `R`. *Significado:* representan la muerte, la vida efímera y el alma del difunto; evolución del *tzompantli*. Suelen llevar el nombre del ser querido en la frente. *Colocación:* decorativas sobre el altar, coloridas con glaseado real.
9. **El Licor ("el trago")** — `R`. *Significado:* para que el difunto recuerde los momentos de alegría que vivió. *Colocación:* tequila, mezcal, cerveza o bebidas artesanales que disfrutaba en vida.
10. **Cruz de Ceniza** — `R`. *Significado:* expiación y purificación; ayuda al alma a expiar culpas y salir del purgatorio para visitar a los suyos. *Colocación:* cruz grande de ceniza en el altar.
11. **Papel Picado** — `R`. *Significado:* elemento aire y fragilidad de la vida; al moverse con la brisa indica que las almas han llegado. Cada color tiene significado (naranja=sol/vida; morado=luto; blanco=pureza/niños; negro=inframundo; rosa=celebración; rojo=vida/sacrificio; azul=fallecidos por agua). *Colocación:* colgado sobre el altar y el espacio.
12. **La Vara (árbol)** — `R`. *Significado:* herramienta espiritual para que el difunto se defienda de malos espíritus y supere obstáculos en su viaje; considerado elemento de vida. *Colocación:* puede ser cualquier árbol/rama.
13. **El Petate** — `R`. *Significado:* cama tejida de palma para que las ánimas descansen tras su travesía; también funciona como mantel/base de la ofrenda y une el mundo terrenal con el espiritual. *Colocación:* como base/mantel; puede adornarse con flores.
14. **Objetos Personales** — `R`. *Significado:* conectan el alma con su identidad y lo que apreciaba en vida. *Colocación:* prendas, objetos de uso cotidiano, artículos de pasatiempos y, para niños, juguetes.
15. **Pan de Muerto** — `R`. *Significado:* el más emblemático; ciclo de vida y muerte, fraternidad y ofrecimiento de alimento. La forma circular=eternidad; la esfera superior=cráneo/alma; las canillas=huesos y lágrimas (puntos cardinales); azúcar/ajonjolí=dulzura de la vida. *Colocación:* sobre la ofrenda.
16. **Comida y Bebida** — `R`. *Significado:* platillos, bebidas y dulces favoritos del difunto para deleitarlo en su visita. *Colocación:* sobre el petate/mantel.
17. **Objetos Religiosos o Místicos** — `R`. *Significado:* si el difunto era devoto, se incluyen rosarios, crucifijos, figuras de santos o amuletos. *Colocación:* en los niveles superiores junto a las imágenes.
18. **Crucifijo** — `R`. *Significado:* simboliza la fe y sirve para que el ánima expíe sus culpas pendientes. *Colocación:* en el nivel superior, mirando al frente, junto a las fotografías; de madera, resina o metal.
19. **El Arco** — `R`. *Significado:* puerta/umbral que une el mundo de los vivos con el más allá y da la bienvenida a las almas. *Colocación:* en la cúspide/último nivel o al frente del altar; tradicionalmente de carrizo, palma o madera flexible, con cruz de palma al centro, adornado con cempasúchil (frutas opcionales).
20. **El Camino** — `R`. *Significado:* sendero que guía a las almas desde el más allá hasta el altar y de regreso. *Colocación:* camino de pétalos de cempasúchil (opcional sobre base de aserrín) desde el último escalón hasta el arco de bienvenida, acompañado de veladoras encendidas a los lados.
21. **Vasijas de Metal y de Barro** — `D`. *Significado/uso:* elementos de decoración del altar. *Colocación:* al final, como parte de la decoración general.

> **Nota de evaluación:** algunos elementos pueden repetirse o colocarse en otras áreas del
> altar, siempre que cumplan con su norma de colocación, sin que ello interfiera con la
> evaluación.

---

## 9. Constancias y resultados

`ConstanciaService` centraliza generación y envío (PDF horizontal carta con QuestPDF,
logotipos UABC/FIM/APFI, ilustraciones temáticas, fecha dinámica en español de México y
firmas oficiales).

**Cambios clave:**
- **Todos los participantes reciben constancia**, sin importar la nota final (se elimina el umbral de 9.0 como condición de envío).
- Las constancias muestran el **lugar/posición del equipo** cuando aplique (1.°, 2.° o 3.°), calculado **por carrera**.
- **Constancia para el maestro encargado** del equipo.
- **Constancia para el juez** por su participación.
- Tipos: **grupal** (menciona al equipo) e **individual** (menciona al integrante).
- Rutas existentes a conservar/ajustar: descarga grupal, descarga de individuales (ZIP), envío de constancias, y descarga propia del participante desde el portal.
- Envío **manual** disponible desde historial/detalle para reenviar a cualquier equipo.

---

## 10. Vista del juez (panel operativo)

- **Por carrera**: ver cuántos equipos hay, cuáles ya están calificados y cuáles faltan.
- Al entrar al altar de una carrera, mostrar **la ubicación física** a la que el juez debe ir.
- Acceso al **recorrido en PDF** subido por el administrador.
- Crear/editar evaluaciones en estado `Preliminar` y cerrarlas con confirmación.

---

## 11. Configuración del periodo y administración

- **Fechas límite** por periodo: inscripción de equipos y fecha en que los equipos deben tener todos los requisitos.
- Subida del **recorrido PDF**.
- Gestión de jueces (alta, edición, soft delete, reactivación, promoción a admin).
- Gestión de registrantes y equipos (sección 6).
- (Opcional) edición de pesos de calificación.

---

## 12. Reportería — Reporte de Cierre de Periodo

Al finalizar cada periodo académico (`YYYY-1` / `YYYY-2`), el sistema genera un **reporte
agregado de cierre** con todos los conteos y estadísticas disponibles del periodo, para
uso institucional (dirección de la FIM, APFI). Accesible desde
`/AltarAdmin/ReportePeriodo/{periodo}`, con exportación a PDF y/o Excel.

### 12.1 Métricas a incluir

**Participación general**
- Total de equipos inscritos, y cuántos completaron la Ficha de Registro / cuántos quedaron incompletos.
- Total de registrantes por tipo (Alumno / Maestro / Administrativo).
- Total de evaluaciones en estado `Preliminar` vs. `Final` al momento del corte.
- Promedio de integrantes por equipo.

**Distribución académica**
- Equipos por **carrera** (conteo y % del total).
- Registrantes (alumnos) por **carrera**.
- Equipos por **tipo de altar** (Tradicional / Niños).
- Equipos por **número de niveles** (3 / 7).
- Equipos que declararon **Catrina** vs. los que no.

**Resultados de evaluación**
- Promedio, máximo y mínimo de `NotaFinal` — general y desglosado por carrera.
- Promedio de cada componente por separado: Objetivo Cultural, Esencia y Personalidad,
  Valoración General, Distribución por Niveles, Narrador.
- Promedio de `NotaCatrina` (entre los equipos que sí la evaluaron).
- 1°, 2° y 3° lugar por carrera (altar) y por carrera (Catrina).
- Elemento(s) del checklist con más incidencia de "No presente" y de "Muy satisfactorio"
  (para detectar qué se está descuidando o qué se está haciendo consistentemente bien).

**Participación de jueces y maestros**
- Total de jueces activos que evaluaron en el periodo, y evaluaciones por juez.
- Total de maestros encargados distintos participando.

**Estadísticas demográficas (dato sensible — ver regla de umbral en 12.2)**
- Distribución de **género** de los registrantes participantes.
- Distribución de **autodescripción cultural** de los registrantes participantes.

### 12.2 Regla de protección de datos sensibles (umbral de agrupación)

`AutodescripcionCultural` (y, en menor medida, `Genero`) son **datos personales
sensibles** bajo la legislación mexicana de protección de datos (equiparables a origen
étnico/racial). Mostrar un conteo exacto y bajo en una categoría puede **des-anonimizar**
a una persona específica dentro de un periodo/carrera reducido (ej. "1 persona
autodescrita como X en la carrera Y" es identificable por cualquiera que conozca los
equipos de esa carrera).

**Regla obligatoria:** en cualquier reporte, tabla o gráfico donde se desglosen
`Genero` o `AutodescripcionCultural`, **toda categoría con menos de `N = 5` personas en el
universo mostrado se agrupa bajo la etiqueta "Otros / grupo reducido"** en vez de
mostrarse individualmente con su conteo real. Esto aplica tanto al reporte general del
periodo como a cualquier corte más fino (por carrera, por tipo de altar, etc.) — el
umbral se evalúa sobre el universo específico que se está mostrando en cada tabla, no
solo sobre el total del periodo.

- El valor `N = 5` es el punto de partida propuesto; debe ser **configurable** desde
  `ConfiguracionPeriodo` (o una configuración global de sistema) para que el comité pueda
  ajustarlo sin requerir cambio de código.
- Esta regla aplica **únicamente a las vistas/exportaciones de reportería agregada**. No
  aplica a las vistas administrativas operativas donde un admin gestiona registrantes
  individuales (ahí sí necesita ver el dato real de una persona puntual para su gestión).
- `AutodescripcionCultural`, al ser texto libre (sección 4.1), debe normalizarse
  (agrupar variantes de escritura equivalentes) antes de aplicar el conteo y el umbral —
  definir con el comité si esa normalización es manual (catálogo de equivalencias
  curado por el admin) o heurística; no debe inventarse una taxonomía cerrada que no
  esté en `vision.md`.

### 12.3 Consideraciones adicionales
- El reporte debe poder generarse para periodos **cerrados** (históricos) y no solo para
  el periodo activo, para permitir comparativas año contra año a futuro.
- El reporte agregado en sí (sin desglose por individuo) no contiene datos que permitan
  identificar a una persona específica más allá de lo ya expuesto en rankings públicos
  (nombre de equipo/altar, lugar) — los rankings de equipo **no** están sujetos al umbral
  de la sección 12.2 porque no son el dato sensible en cuestión.

---

## 13. Decisiones confirmadas y pendientes

### Confirmado por el comité
1. **Pesos de la nota final**: se mantiene la ponderación `C·30% + P·30% + G·20% + N·10% + R·10%` como punto de partida, **editable desde el sistema** (sección 7.7).
2. **Catrina**: es una **categoría de evaluación aparte**, con su propio ranking por carrera; no suma a `NotaFinal` del altar (sección 7.5).
3. **Mapeo elemento → nivel sugerido**: se confirma que el catálogo de elementos (sección 8) debe distribuirse por nivel para los altares de 3 y 7 niveles.
4. **Ranking por carrera**: el lugar (1°/2°/3°) se calcula con base en la `Carrera` **del equipo/altar**, no de los integrantes — un equipo puede tener integrantes de distintas carreras, pero compite dentro de la carrera a la que pertenece su altar.
5. **Identificador por tipo de registrante**: Alumno = matrícula de **7 dígitos**; Maestro y Administrativo = matrícula de **5 dígitos** (mismo formato, distinguidos por `Tipo`).
6. **Autodescripción cultural**: campo de **texto libre** (no catálogo de opciones), tal como se redactó en `programa_altar.docx`.
7. **Catálogo de género**: queda a criterio de diseño del equipo de desarrollo — ver propuesta en el Apéndice B, editable desde el admin.
8. **Catálogo oficial de carreras de la FIM**: confirmado por Salch, 13 programas (Apéndice C), modelado como tabla editable desde el admin.
9. **Reporte de cierre de periodo**: se implementa con el alcance completo de la sección 12.1, y con **umbral de agrupación `N = 5`** (propuesto y aceptado como punto de partida, configurable) para proteger `Genero` y `AutodescripcionCultural` de des-anonimización en cortes reducidos (sección 12.2).

### Pendiente de confirmación
10. **Valores exactos de la escala de satisfacción** (0 / 0.5 / 0.75 / 1.0) y **pesos de elementos rituales vs. decorativos**: quedan como en la propuesta de la sección 7.2, editables desde `ConfiguracionPeriodo`. Confirmar si el comité quiere ajustar estos valores numéricos.
11. **Regla de empates** en el ranking por carrera: no especificada; definir criterio de desempate (p. ej. mayor `Objetivo Cultural`, o evaluación conjunta de jueces).
12. **Normalización de `AutodescripcionCultural`** para efectos de conteo en el reporte (sección 12.2): definir si es manual (catálogo de equivalencias curado por el admin) o heurística.

---

## 14. Stack técnico (sin cambios)

- **Framework:** ASP.NET Core MVC 8.0
- **Datos:** Entity Framework Core + SQL Server (LocalDB en desarrollo)
- **PDF:** QuestPDF (licencia comunitaria)
- **UI:** HTML5, CSS3, Bootstrap 5, FontAwesome 6 (temática "Noche de Altar")
- **Correo:** Gmail SMTP
- **Auth participantes:** Google OAuth restringido a `@uabc.edu.mx`
- **Periodos:** `YYYY-1` (ene–jul) / `YYYY-2` (ago–dic)
- **Archivos privados** *(recorrido PDF)*: se mantiene el almacenamiento para
  `ConfiguracionPeriodo.RecorridoPdf`, subido por el admin. Validar tipo de archivo y
  tamaño máximo; definir si se guarda en disco del servidor, Azure Blob Storage, o en la
  base de datos — a decidir según el destino final de despliegue en Azure App Service.

---

## Apéndice — Elementos retirados de la versión anterior

- Nivel **2** (solo quedan 3 o 7).
- Tipo de altar **Mascotas** (quedan Tradicional y Niños).
- **Check binario + bonus temático** (sustituidos por la escala de satisfacción).
- **Umbral de 9.0** como condición para enviar constancias (ahora se envían a todos).

---

## Apéndice B — Catálogo de género (propuesta editable)

Catálogo simple, gestionable desde `/Admin` sin requerir despliegue de código (tabla
`CatalogoGenero` con alta/baja de opciones). Propuesta inicial:

- Masculino
- Femenino
- No binario
- Prefiero no especificar
- Otro *(con campo de texto libre opcional)*

---

## Apéndice C — Catálogo de carreras FIM (oficial)

Listado oficial confirmado por Salch, tomado del menú "Programas Educativos" del sitio de
la FIM. Se modela `Carrera` como **tabla editable desde el admin** (no `enum` fijo en
código) para poder ajustar nombres o agregar programas nuevos sin tocar el código fuente.

1. Lic. en Sistemas Computacionales
2. Bioingeniero
3. Ing. Aeroespacial
4. Ing. Civil
5. Ing. en Computación
6. Ing. en Electrónica
7. Ing. Eléctrico
8. Ing. en Energías Renovables
9. Ing. Industrial
10. Ing. Mecánico
11. Ing. en Mecatrónica
12. Ing. en Semiconductores y Microelectrónica
13. Ing. de Datos e Inteligencia Artificial
