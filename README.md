# AltarWeb

Sistema de gestión y evaluación para el Concurso de Altares de Muertos de la Facultad de Ingeniería (FIM) de la Universidad Autónoma de Baja California (UABC).

AltarWeb permite a los jueces registrar evaluaciones de altares bajo los nuevos criterios culturales, calcular puntajes por nivel y por elemento, gestionar el registro de participantes (alumnos, maestros y administrativos), administrar equipos por carrera y periodo, y generar y enviar constancias oficiales en PDF a todos los participantes, maestros encargados y jueces.

La especificación funcional completa vive en [`vision.md`](vision.md).

---

## 🛠️ Tecnologías utilizadas

- **Framework:** ASP.NET Core MVC 8.0
- **Persistencia de datos:** Entity Framework Core + SQL Server (LocalDB en desarrollo)
- **Generación de documentos:** [QuestPDF](https://www.questpdf.com/) (licencia comunitaria)
- **Interfaz:** HTML5, CSS3 y FontAwesome 6, con un sistema de diseño propio ("Noche de Altar") que incluye **tema claro, oscuro y automático (según el sistema operativo)**
- **Correo:** SMTP (Gmail) para el envío de constancias
- **Autenticación:** sesión local con contraseña (PBKDF2 vía `Microsoft.AspNetCore.Identity.PasswordHasher`) o Google OAuth restringido a `@uabc.edu.mx`, tanto para participantes como para jueces

---

## 🔑 Acceso y roles

El landing (`/Registro/Login`) tiene dos pestañas: **Jueces/Admin** y **Registro**.

### Jueces y administradores
- Inician sesión con su usuario o su correo institucional (`/Acceso/Login`), o con **"Continuar con Google"**.
- **Auto-registro de jueces** (`/Registro/SignupJuez`, local o Google): un juez nuevo captura nombre, correo institucional y matrícula/número de empleado (5 dígitos). Si entra por Google, primero se autentica y luego solo completa la matrícula (`/Registro/CompletarGoogleJuez`), ya que Google no la proporciona.
- Toda cuenta auto-registrada queda **pendiente** hasta que un administrador la aprueba desde `/AltarAdmin/Jueces` (badge "Pendiente" + botón "Aprobar"). Mientras esté pendiente, el login queda bloqueado con un mensaje explícito.
- El administrador también puede crear jueces manualmente desde `/AltarAdmin/CrearJuez` (quedan activos de inmediato, sin aprobación).
- **Rol Juez:** crea y edita evaluaciones (`Preliminar` → `Final`), consulta el historial por periodo, ve el avance por carrera clasificado en *Final / En preliminar / Sin calificar*, consulta el recorrido en PDF (embebido en la página) y descarga/envía constancias.
- **Rol Administrador:** todo lo del juez, más gestión de jueces (alta, aprobación, soft-delete/reactivación), gestión de registrantes y equipos, y configuración del periodo (fechas límite, **subida del recorrido en PDF**, pesos de calificación).

### Participantes (Portal de Registro)
- Alumnos (matrícula de **7 dígitos**), maestros y administrativos (matrícula/no. de empleado de **5 dígitos**) se registran en `/Registro/Signup` con correo institucional y contraseña, o con **Google OAuth**.
- Si el correo de Google no coincide con ningún registrante existente, se pide completar los datos que Google no entrega (identificador, teléfono, género, carrera, autodescripción cultural) en `/Registro/CompletarGoogle` antes de entrar al portal.
- Dashboard (`/Registro/Dashboard`): estado del equipo, estado de la Ficha de Registro, integrantes, maestro encargado, catálogo de elementos requeridos y descarga de constancia propia una vez que la evaluación del equipo es `Final`.

---

## 👥 Equipos y Ficha de Registro del Altar

- Un registrante crea su equipo (`/Registro/CrearEquipo`) y completa la **Ficha de Registro del Altar** (`/Registro/Ficha`): nombre del grupo, nombre del altar, difunto (nombre y fecha de defunción), programa educativo, lugar de exposición, maestro encargado (por matrícula; si aún no está registrado queda pendiente hasta que él mismo se registre) y si el equipo **hará Catrina o no**.
- La ficha es editable mientras el equipo **no tenga ninguna evaluación registrada** (ni `Preliminar` ni `Final`) **y** no haya pasado la fecha límite de requisitos. En cuanto existe una calificación —aunque sea preliminar— la ficha e integrantes quedan cerrados, para que el equipo no cambie respecto a lo que el juez ya evaluó. (La descarga de constancia sí sigue atada al estado `Final`.)
- El organizador administra integrantes y designa un narrador; en cuanto el equipo tiene una evaluación (preliminar o final), la lista de integrantes queda cerrada para conservar la consistencia con lo evaluado y las constancias.
- Un registrante solo puede pertenecer a un equipo activo por periodo (`YYYY-1` / `YYYY-2`).
- **Fechas límite configurables por el administrador** desde `/AltarAdmin/Configuracion` (aplican por periodo académico):
  - **Inscripción de equipos:** pasada esa fecha, ya no se pueden crear equipos nuevos (`/Registro/CrearEquipo` se bloquea con un mensaje explicativo). No afecta el registro de participantes, solo la creación de equipos.
  - **Requisitos completos:** pasada esa fecha, la Ficha de Registro deja de ser editable (independientemente de si el equipo ya fue evaluado o no). Si no hay fecha configurada, no aplica ningún límite.

---

## 📝 Proceso de evaluación

Desde `/AltarEvaluacion/NuevaEvaluacion` el juez busca el equipo por carrera (tiempo real) y la vista precarga en solo lectura todos los datos de la Ficha de Registro.

- **Niveles:** el altar es de **3 o 7 niveles**; el checklist se agrupa por nivel y el juez asigna una nota de **Distribución por Niveles**.
- **Checklist de 21 elementos** (catálogo documentado en `vision.md` §8) con **escala de satisfacción** por elemento (No presente / Poco / Satisfactorio / Muy satisfactorio) en vez de check binario, más un marcador de "¿tematizado?" que otorga un bono configurable. Cada elemento tiene un ícono **(i)** con su mini-manual (significado y colocación).
- **Categorías de calificación** (0–10 cada una): **Objetivo Cultural** (sugerido automáticamente a partir del Puntaje de Elementos), **Esencia y Personalidad**, **Valoración General**, **Distribución por Niveles** y **Narrador**.
- **Catrina:** el equipo declara en su Ficha de Registro si hará Catrina o no; la pestaña de evaluación de Catrina solo aparece si el equipo lo declaró. Es una **categoría independiente** con su propio ranking por carrera — no suma a la Nota Final del altar.
- **Estados:** `Preliminar` (editable las veces que se necesite) y `Final` (requiere confirmación explícita; solo las evaluaciones `Final` cuentan para ranking, constancias y reportería).

### Fórmula de la Nota Final

Pesos editables desde `/AltarAdmin/Configuracion` (deben sumar 100%):

```
NotaFinal = ObjetivoCultural·30% + EsenciaPersonalidad·30% + ValoracionGeneral·20%
          + DistribucionPorNiveles·10% + Narrador·10%
```

El **Puntaje de Elementos** alimenta como sugerencia al Objetivo Cultural, en vez de sumar aparte. El lugar (1°/2°/3°) se calcula por carrera al cerrar una evaluación como `Final`.

---

## 🖨️ Constancias

`ConstanciaService` centraliza la generación (QuestPDF, carta horizontal, logos UABC/FIM/APFI, fecha dinámica en español de México, firmas oficiales) y el envío por correo. Se generan **para todos los participantes, sin importar la nota**, una vez que la evaluación del equipo está en estado `Final`:

- **Grupal** (`/Constancia/DescargarGrupal/{evaluacionId}`): a nombre del equipo.
- **Individuales** (`/Constancia/DescargarIndividuales/{evaluacionId}`): ZIP con un PDF por integrante.
- **Maestro encargado** (`/Constancia/DescargarMaestro/{evaluacionId}`).
- **Juez** (`/Constancia/DescargarJuez/{evaluacionId}`).
- **Envío manual por correo** (`/Constancia/EnviarTodas/{evaluacionId}`) desde el detalle de la evaluación.
- El propio participante descarga la suya desde su dashboard (`/Constancia/DescargarMia`).

Las constancias muestran el lugar/posición del equipo cuando aplica.

---

## 🎨 Tema claro / oscuro / sistema

El selector de tema (ícono de sol/luna/escritorio, visible en el sidebar y en el landing) permite elegir Claro, Oscuro o Sistema. La preferencia se guarda en `localStorage` y, en modo Sistema, sigue en vivo los cambios de `prefers-color-scheme` del sistema operativo, sin parpadeo al recargar la página.

---

## 📊 Reporte de Cierre de Periodo

`/AltarAdmin/ReportePeriodo/{periodo}` (solo Administrador) genera un reporte agregado del periodo con participación general, distribución académica, resultados de evaluación (solo evaluaciones `Final`), participación de jueces/maestros y estadísticas demográficas. Puede consultarse para el periodo activo o para cualquier periodo histórico cerrado (selector en la parte superior), y descargarse en PDF (`ReportePeriodoService`, vía QuestPDF).

**Protección de datos sensibles:** cualquier categoría de **Género** o **Autodescripción cultural** con menos de `N` personas en el corte que se esté mostrando (general o desglosado, p. ej. por carrera) se agrupa como *"Otros / grupo reducido"* en vez de mostrarse con su conteo real, para evitar identificar a una persona específica en un corte pequeño. `N` (por defecto `5`) es configurable por periodo desde `/AltarAdmin/Configuracion` (`ConfiguracionPeriodo.UmbralAgrupacionDemografica`). Esta regla vive en `PrivacidadReporteHelper` y aplica **únicamente** a esta reportería agregada — las pantallas de gestión individual de registrantes (`/AltarAdmin/RegistrantesYEquipos`) siguen mostrando el dato real de una persona puntual.

`AutodescripcionCultural` es texto libre, así que antes de contar se normaliza con `Trim().ToLowerInvariant()` para unir variantes triviales de escritura (`"Migrante"` / `"migrante "` → `"migrante"`). Esta normalización es una **decisión temporal**, no definitiva — el comité aún debe decidir si prefiere una normalización manual (catálogo de equivalencias) o heurística (vision.md §13).

---

## 📅 Periodos académicos

Las evaluaciones y equipos se agrupan automáticamente según el ciclo escolar:
- Enero a julio: `YYYY-1`
- Agosto a diciembre: `YYYY-2`

---

## 🚀 Configuración e instalación

### Requisitos previos
- **.NET 8.0 SDK** o superior.
- **SQL Server** (LocalDB incluido con la carga de trabajo de desarrollo web de Visual Studio).
- Conexión a internet para NuGet, Google OAuth y el envío de correo SMTP.

### Pasos

1. **Clonar el repositorio:**
   ```bash
   git clone https://github.com/AbrahamHamedFloresCabanillas/AltarWeb.git
   cd AltarWeb
   ```

2. **Configurar `AltarWeb/appsettings.json`** (no se versiona; créalo a partir de este ejemplo):
   ```json
   {
     "ConnectionStrings": {
       "AltarWebContext": "Server=(localdb)\\MSSQLLocalDB;Database=AltarWebContext;Trusted_Connection=True;MultipleActiveResultSets=true"
     },
     "GoogleAuth": {
       "ClientId": "TU_CLIENT_ID_AQUI.apps.googleusercontent.com",
       "ClientSecret": "TU_CLIENT_SECRET_AQUI"
     },
     "Smtp": {
       "Host": "smtp.gmail.com",
       "Port": 587,
       "EnableSsl": true,
       "User": "TU_CORREO_SMTP_AQUI",
       "Password": "TU_PASSWORD_APP_AQUI",
       "FromName": "Concurso Altares FIM"
     },
     "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
     "AllowedHosts": "*"
   }
   ```
   URI de callback de Google (registrar en Google Cloud Console): `http://localhost:5185/Registro/google-callback`.

3. **Ejecutar migraciones:**
   ```bash
   dotnet tool install --global dotnet-ef   # si no lo tienes instalado
   dotnet ef database update --project AltarWeb/AltarWeb
   ```

4. **Ejecutar la aplicación:**
   ```bash
   dotnet run --project AltarWeb/AltarWeb --urls "http://localhost:5185"
   ```
   Al iniciar por primera vez se crea un administrador semilla. La contraseña **no** es fija: se toma de
   la variable de entorno `SEED_ADMIN_PASSWORD` si está definida, o se genera aleatoria y se imprime
   **una sola vez** en el log de arranque. En ambos casos, la cuenta queda marcada con
   `DebeCambiarPassword=true`: el primer login fuerza el cambio antes de permitir cualquier otra acción
   (`/Acceso/CambiarPasswordObligatorio`). El usuario semilla es `abram` salvo que se defina
   `SEED_ADMIN_USUARIO`.

### Seguridad — estado real del sistema (actualizado tras auditoría de seguridad, 2026-07-04)

`AUDITORIA_SEGURIDAD.md` documenta la auditoría completa (19 hallazgos) y el detalle de cada corrección.
Resumen del estado actual:

- **Contraseñas:** PBKDF2 (`Microsoft.AspNetCore.Identity.PasswordHasher`) para `Registrante` y `Juez`,
  con migración automática *rehash-on-login* de cualquier hash SHA-256 legado — no rompe cuentas
  existentes.
- **Google OAuth:** el email/nombre verificados por Google se guardan server-side (en `Session`, token
  de un solo uso con vida de 10 minutos) al llegar al callback; los formularios de completar registro
  **ignoran** cualquier correo que venga posteado desde el cliente.
- **CSRF:** filtro global `AutoValidateAntiforgeryTokenAttribute` en todos los POST/PUT/DELETE.
- **Cookies de sesión:** `Secure` + `HttpOnly` + `SameSite=Lax` explícitos.
- **Cabeceras de seguridad:** `X-Content-Type-Options`, `Referrer-Policy`, `Content-Security-Policy`
  básica, `ForwardedHeaders` configurado para Azure App Service (o cualquier proxy que termine TLS).
  **Deuda técnica conocida:** la CSP usa `script-src 'self' 'unsafe-inline'` porque el layout tiene
  `<script>` inline (tema, sidebar, datepicker) — esto reduce el valor de la CSP contra XSS. Cerrarlo
  requeriría mover esos scripts a archivos estáticos o usar nonces por request; queda pendiente.
- **Rate limiting:** 10 intentos/minuto por IP en las rutas de login (`/Acceso/Login`, `/Registro/Login`,
  callback de Google). **Nota operativa:** el límite es por IP; en una red institucional con NAT (varios
  alumnos compartiendo la misma IP pública, como suele pasar en la red de la FIM), esto puede afectar a
  usuarios legítimos si hay fricción simultánea. Vigilar en producción y ajustar el umbral si se detectan
  falsos positivos.
- **Logging:** intentos de login fallidos y accesos denegados por rol se registran (`ILogger`, nivel
  Warning) con usuario/correo intentado e IP — nunca contraseñas ni tokens.
- **Concurrencia:** índice único a nivel de BD para "un registrante por equipo activo por periodo" y para
  "una evaluación por equipo", con manejo de errores para mostrar un mensaje amable en vez de un 500 ante
  una carrera real (verificado con requests concurrentes reales, no solo lectura de código).
- **PDF del Reporte de Cierre:** marca de agua "CONFIDENCIAL" y leyenda con fecha/usuario generador en
  header y footer de cada página.
- **`CrearJuez`:** usa un ViewModel dedicado (no bindea la entidad EF completa).
- **Cierre de evaluación `Final`:** requiere un Narrador designado y la Ficha de Registro completa
  (incluye maestro encargado vinculado); se rechaza con mensaje si falta alguno.
- **Dependencias:** la herramienta de scaffolding (`Microsoft.VisualStudio.Web.CodeGeneration.Design`,
  que arrastraba `NuGet.Packaging`/`NuGet.Protocol` vulnerables) solo se incluye en builds `Debug`; un
  `dotnet publish -c Release` no la empaqueta.

### Checklist de despliegue a producción (Azure App Service u otro proxy)

- [ ] Fijar `ASPNETCORE_ENVIRONMENT=Production` explícitamente en la configuración del App Service (en
  `Development` se muestra la Developer Exception Page con stack trace; no asumir el default).
- [ ] Definir `SEED_ADMIN_PASSWORD` (o dejar que se genere aleatoria y capturarla del log de arranque
  antes de que rote el log).
- [ ] Confirmar que el proxy/App Service termina TLS y que `ForwardedHeaders` refleja `https` en
  `Request.Scheme` (sin esto, las cookies `Secure` y el callback de Google no funcionan tras el proxy).
- [ ] Sustituir `GoogleAuth` y `Smtp` en `appsettings.json` con credenciales reales.
- [ ] Publicar con `dotnet publish -c Release` (no `Debug`) para excluir la herramienta de scaffolding.

### Decisiones de producto pendientes (no técnicas)

- **Verificación de correo en registro local** (`PRIV-05` en `AUDITORIA_SEGURIDAD.md`): el registro local
  (`/Registro/Signup`, `/Registro/SignupJuez`) no confirma que quien se registra controla el correo
  institucional. **Decisión explícita del propietario del proyecto (2026-07-04): aceptar el riesgo por
  ahora, sin cambios de código** — no se implementó verificación de correo ni restricción a Google-only
  en esta ronda. Pendiente de revisión futura.
- **Normalización de `AutodescripcionCultural`** (`PRIV-04`): se usa `Trim().ToLowerInvariant()` como
  solución temporal para agrupar variantes de escritura antes de contar (vision.md §13). El comité aún
  debe decidir entre una normalización manual (catálogo de equivalencias) o heurística. No se tocó en
  esta ronda.
- **Autenticación por `[Authorize]`/`ClaimsPrincipal`** (`SEC-08`): hoy cada acción protegida hace un
  chequeo manual de sesión (`EstaAutenticado()`/`EsAdmin()`); la cobertura actual es completa (auditoría
  confirmó que no hay ningún endpoint alcanzable sin sesión), pero depende de que cada acción nueva
  recuerde añadir el chequeo. Migrar a un filtro/atributo de autorización central es una mejora de
  robustez de esfuerzo alto, diferida post-lanzamiento — no es un hueco de seguridad activo.
- **`ReportePeriodoPdf` sin rate-limit propio** (`PRIV-03`): cerrado como *aceptable por paridad* — el
  gate (solo Admin autenticado) es idéntico al del resto del backoffice; no amerita un control distinto.
