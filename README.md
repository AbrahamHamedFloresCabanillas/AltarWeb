# AltarWeb

Sistema de gestión y evaluación para el Concurso de Altares de Muertos de la Facultad de Ingeniería (FIM) de la Universidad Autónoma de Baja California (UABC). 

Este aplicativo web permite a los jueces registrar evaluaciones de altares de muertos, calcular puntajes basados en criterios tradicionales y de personalización, generar constancias oficiales de participación automatizadas en formato PDF, y enviar los resultados de forma automática a los estudiantes participantes.

---

## 🛠️ Tecnologías Utilizadas

El proyecto está construido bajo un stack robusto y moderno:
- **Framework Principal:** ASP.NET Core MVC 8.0
- **Persistencia de Datos:** Entity Framework Core con soporte para SQL Server (LocalDB en desarrollo)
- **Generación de Documentos:** [QuestPDF](https://www.questpdf.com/) (Licencia Comunitaria)
- **Interfaz de Usuario:** HTML5, CSS3, Bootstrap 5 y FontAwesome 6 para un diseño responsivo y moderno (con soporte de temas y elementos visuales adaptados a la festividad)
- **Servicio de Correo:** Integración con Gmail SMTP para la entrega de resultados y constancias.

---

## 🔑 Gestión de Accesos y Roles (RBAC)

El sistema cuenta con un control de accesos basado en roles para proteger las secciones administrativas del concurso:

### 1. Rol: Juez
El perfil operativo del sistema. Sus funciones son:
- **Iniciar y Cerrar Sesión:** Autenticación a través del controlador de acceso.
- **Crear Nueva Evaluación:** Registrar los datos de los altares concursantes.
- **Consultar Historial:** Ver las evaluaciones realizadas organizadas y agrupadas por periodo académico.
- **Visualizar Detalles:** Ver el desglose detallado de los puntajes de cualquier altar evaluado.
- **Enviar Resultados Manuales:** Volver a detonar el envío de correos con la constancia PDF en caso de ser necesario.
- **Descargar Constancia:** Descargar de forma local el archivo PDF de agradecimiento/participación.

### 2. Rol: Administrador (Admin)
Tiene control total sobre el sistema. Además de las funciones del Juez, cuenta con:
- **Gestión de Jueces:**
  - Crear nuevos usuarios con rol Juez o Administrador.
  - Editar información de jueces existentes (nombre completo, usuario, rol y contraseña).
  - Desactivar jueces mediante **Soft Delete** (desactivación lógica). Esto garantiza que el historial y las evaluaciones realizadas por dicho juez no se pierdan (se asignan de forma segura a `NULL` en la relación de base de datos, manteniendo el snapshot de su nombre en la evaluación).
  - Reactivar jueces previamente desactivados.
  - Promover jueces existentes al rol de Administrador.
- **Gestión de Estudiantes y Equipos:**
  - Visualizar la lista completa de alumnos auto-registrados en el portal de alumnos (`/Admin/Alumnos`).
  - Desactivar estudiantes de forma individual (**Soft Delete**) en caso de requerirse por incidencias. Sus evaluaciones previas se conservan intactas.
  - Reactivar estudiantes desactivados.
  - Administrar equipos del periodo actual (`/Admin/Equipos`) e histórico (`/Admin/EquiposHistorico`).
  - Editar nombre e integrantes de equipos desde `/Admin/EditarEquipo/{id}`.

---

## 📝 Proceso de Evaluación y Fórmulas de Calificación

El registro de una nueva evaluación es el núcleo del sistema, accesible desde la vista `Evaluacion/Crear` mediante una interfaz organizada por pestañas y tablas dinámicas:

### 1. Selección de Equipo
- **Búsqueda en Tiempo Real:** El juez escribe el nombre del equipo en un campo de búsqueda y el sistema devuelve coincidencias del periodo académico actual (los administradores pueden buscar también en periodos anteriores).
- **Integrantes en Modo Lectura:** Al seleccionar un equipo, la vista precarga automáticamente los integrantes registrados por los propios alumnos desde el portal. El juez no captura integrantes manualmente.
- **Validación de Duplicado:** Si el equipo seleccionado ya cuenta con una evaluación registrada, el sistema lo indica con un error antes de procesar el formulario.
- **Gestión de equipos e integrantes:** Los equipos y sus integrantes son creados y administrados por los propios alumnos desde el Portal de Alumnos (`/Alumno/MiEquipo`) o por administradores desde `/Admin/EditarEquipo/{id}`.

### 2. Pestaña: Evaluación de Altar
- **Datos Generales:** Nombre del difunto a quien se dedica el altar, Niveles (2, 3 o 7 Niveles) y Tipo de Altar (Tradicional, Niños o Mascotas).
- **Checklist Tradicional:** Evaluación física de los 10 elementos indispensables:
  1. Foto del Difunto
  2. Velas / Luz
  3. Flor de Cempasúchil
  4. Papel Picado
  5. Pan de Muerto
  6. Vaso de Agua
  7. Sal
  8. Incienso / Copal
  9. Calaveritas
  10. Objetos Personales
- **Bonus Temáticos (Personalización):** Cada elemento del checklist tradicional cuenta con una opción de *"¿Está Tematizado?"*. Al marcarse, incrementa los **Bonus Temáticos** en `1` punto por cada elemento tematizado (máximo de 10 puntos extra acumulables).
- **Sección de Calificación:**
  - Hobbies / Temática: Espacio de texto para describir la justificación del diseño.
  - Nota de Tradición (0 a 10)
  - Nota de Personalización (0 a 10)
  - Nota de Estética (0 a 10)

### 3. Fórmulas de Cálculo Automático
Al guardar la evaluación, el sistema calcula de forma automática la nota de cada área y la nota final bajo el siguiente algoritmo:
- **Puntaje de Tradición:** Se promedia el número de elementos tradicionales presentes (`enc`, de 0 a 10) con la nota de tradición asignada por el juez:
  $$\text{Nota Tradición Final} = \min\left(10, \frac{\text{Elementos Presentes} + \text{Nota Tradición Juez}}{2}\right)$$
- **Puntaje de Personalización:** Se suma la nota de personalización asignada por el juez con un bono del 50% por cada elemento tematizado registrado:
  $$\text{Nota Personalización Final} = \min\left(10, \text{Nota Personalización Juez} + (\text{Bonus Temáticos} \times 0.5)\right)$$
- **Calificación Final:** Es la suma ponderada de las tres grandes categorías del concurso:
  $$\text{Nota Final} = (\text{Nota Tradición Final} \times 30\%) + (\text{Nota Personalización Final} \times 40\%) + (\text{Nota Estética Juez} \times 30\%)$$

---

## 🖨️ Automatización y Generación de Constancias

### Generación de PDF (QuestPDF)
El sistema genera un diseño horizontal (Landscape) de tamaño carta para las constancias de participación. Incorpora de forma dinámica:
- Logotipos oficiales de la UABC, la Facultad de Ingeniería (FIM) y la Asociación de Profesores (APFI).
- Ilustraciones temáticas tradicionales (Catrina en el fondo inferior izquierdo).
- Texto personalizado de agradecimiento, incluyendo el nombre del equipo y el difunto homenajeado.
- Fecha dinámica en español de México (ej. *Mexicali, Baja California a 01 de Noviembre de 2026*).
- Firmas oficiales de las autoridades del plantel: Directora de la Facultad de Ingeniería y Presidenta de la APFI.

### Envío de Resultados por Correo Electrónico
- **Envío Automático:** Si la evaluación de un equipo obtiene una **Nota Final de 9.0 o superior**, el sistema procesa y envía de forma automática un correo electrónico a cada uno de los integrantes del equipo que cuenten con dirección de correo registrada. Este correo incluye la constancia de participación en PDF adjunta.
- **Envío Manual:** En la sección de historial o detalle, los jueces pueden detonar el envío manual de correos a cualquier equipo con un solo clic.

---

## 📅 Periodos Académicos
El sistema agrupa automáticamente las evaluaciones de acuerdo al ciclo escolar en el que se realizan:
- Evaluaciones creadas de Enero a Julio se marcan como: `YYYY-1`
- Evaluaciones creadas de Agosto a Diciembre se marcan como: `YYYY-2`

Esto permite mantener un histórico limpio y estructurado de los concursos a través de los años.

---

## 🚀 Configuración e Instalación

### Requisitos Previos
- **.NET 8.0 SDK** o superior instalado en el equipo.
- **SQL Server** (LocalDB incluido por defecto al instalar la carga de trabajo de desarrollo web en Visual Studio).
- Conexión a internet activa para descarga de dependencias NuGet y el envío de correos SMTP.

### Pasos para Ejecutar Localmente

1. **Clonar el repositorio:**
   ```bash
   git clone https://github.com/AbrahamHamedFloresCabanillas/AltarWeb.git
   cd AltarWeb
   ```

2. **Configurar la base de datos:**
   El archivo `appsettings.json` viene preconfigurado para apuntar a una base de datos local `AltarWebDb` usando SQL LocalDB. Si necesitas usar una instancia remota o un servidor específico de SQL Server, modifica la cadena de conexión:
   ```json
   "ConnectionStrings": {
     "AltarWebContext": "Server=(localdb)\\mssqllocaldb;Database=AltarWebDb;Trusted_Connection=True;MultipleActiveResultSets=true"
   }
   ```

3. **Ejecutar Migraciones:**
   Genera las tablas y relaciones ejecutando:
   ```bash
   dotnet ef database update --project AltarWeb
   ```
   *(Nota: si no cuentas con las herramientas de Entity Framework, instálalas primero con `dotnet tool install --global dotnet-ef`)*

4. **Inicializar y Ejecutar:**
   Inicia la aplicación con el comando:
   ```bash
   dotnet run --project AltarWeb
   ```
   La consola te indicará los puertos locales (ej. `http://localhost:5000` o `https://localhost:5001`) donde podrás abrir el navegador y comenzar a utilizar el aplicativo.


---

## Portal de Alumnos

El sistema incluye un portal separado en `/Alumno/Login` para alumnos. Los alumnos pueden registrarse con formulario local o iniciar sesion con Google OAuth usando exclusivamente correos `@uabc.edu.mx`.

- Registro local: nombre completo, matricula numerica, correo institucional y contrasena.
- Google OAuth: si el correo institucional no existe, el alumno completa su matricula antes de entrar al dashboard.
- Sesion independiente del portal de jueces/admins.
- Dashboard con estado de equipo y resultado de evaluacion cuando exista.

## Equipos por Periodo

Los alumnos crean equipos asociados al periodo academico actual (`YYYY-1` o `YYYY-2`). Cada alumno puede pertenecer a un solo equipo activo por periodo. El creador queda registrado en `CreadoPorAlumnoId` y tambien como integrante en `AlumnoEquipo`.

Mientras el equipo no tenga una evaluacion registrada, el creador del equipo puede agregar o quitar integrantes desde `/Alumno/MiEquipo`. El creador no puede quitarse a si mismo y, una vez que el equipo fue evaluado, la lista de integrantes queda cerrada para conservar el historial y las constancias.

Los jueces ya no capturan integrantes manualmente al evaluar. En `/Evaluacion/Crear` seleccionan un equipo con busqueda en tiempo real y la vista precarga los integrantes en modo solo lectura.

## Administracion de Alumnos y Equipos

Los administradores cuentan con nuevas rutas:

- `/Admin/Alumnos`: lista, busqueda, desactivacion y reactivacion de alumnos.
- `/Admin/Equipos`: equipos activos del periodo actual.
- `/Admin/EquiposHistorico`: equipos de periodos anteriores o inactivos.
- `/Admin/EditarEquipo/{id}`: cambio de nombre e integrantes del equipo.

El modelo historico `Integrante` se conserva para evaluaciones antiguas. El flujo nuevo usa `Alumno` y `AlumnoEquipo`.

## Constancias Grupales e Individuales

`ConstanciaService` centraliza la generacion y envio:

- Constancia grupal: menciona el nombre del equipo y se envia al creador cuando tiene correo.
- Constancia individual: menciona al integrante y el equipo; se genera para cada integrante con correo.
- `/Evaluacion/DescargarGrupal/{id}` descarga el PDF grupal.
- `/Evaluacion/DescargarIndividuales/{id}` descarga un ZIP con PDFs individuales.
- `/Evaluacion/EnviarConstancias/{id}` envia grupal + individuales.
- `/Alumno/DescargarMiConstancia/{evaluacionId}` permite al alumno descargar solo su constancia si pertenece al equipo y la nota final es al menos 9.0.

## Configuracion de Google OAuth y SMTP

`appsettings.json` contiene placeholders. Para desarrollo se recomienda `dotnet user-secrets` o variables de entorno:

```json
"GoogleAuth": {
  "ClientId": "TU_CLIENT_ID_AQUI",
  "ClientSecret": "TU_CLIENT_SECRET_AQUI"
},
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "EnableSsl": true,
  "User": "TU_CORREO_SMTP_AQUI",
  "Password": "TU_PASSWORD_APP_AQUI",
  "FromName": "Concurso Altares FIM"
}
```

URI de callback local para Google: `http://localhost:5000/Alumno/google-callback`.
