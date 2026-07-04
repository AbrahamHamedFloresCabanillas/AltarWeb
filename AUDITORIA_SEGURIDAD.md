# Brief de Auditoría de Seguridad, Privacidad y Preparación para Producción — AltarWeb

> **Auditor:** Opus 4.8 · **Fecha:** 2026-07-03 · **Destinatario:** sesión de implementación (Sonnet 5)
> **Alcance:** ASP.NET Core MVC 8.0 + EF Core + SQL Server. Auditoría estática completa de los 5 controladores,
> `Program.cs`, servicios, modelos, configuraciones EF, migraciones y seed + **pruebas dinámicas ligeras**
> sobre la app corriendo en `localhost:5185` con LocalDB.
> **No se corrigió nada** en esta sesión. **No se tocó `vision.md` ni `README.md`.** Todo dato QA creado
> durante las pruebas se eliminó (verificado: la BD volvió a sus 3 registrantes reales, sin residuo).

---

## 0. Nota de encuadre — la premisa "dos capas vivas" ya no aplica al 100%

El prompt describe controladores legados (`JuecesController`, `/Alumno/*`, `Evaluacion` vieja) vivos en
paralelo. **La realidad del código actual es distinta y conviene que Sonnet 5 la tenga clara:**

- Solo existen **5 controladores**: `AccesoController`, `RegistroController`, `AltarEvaluacionController`,
  `AltarAdminController`, `ConstanciaController`. **No existe `JuecesController` ni `AlumnoController`.**
- `AccesoController.Login` (POST) **no es un login legado duplicado**: es *el* login real de jueces/admin
  que usa el sistema nuevo (el landing de `RegistroController` redirige a él, el sidebar postea a él).
- Los modelos legados (`Alumno`, `AlumnoEquipo`, `Integrante`, `Evaluacion` vieja) **siguen en el
  `DbContext`** pero **no tienen ningún controlador/ruta HTTP que los exponga** → no son superficie de
  ataque HTTP hoy. Son datos durmientes (deuda de esquema, no de seguridad).
- El "texto plano" que menciona `current_state.md` (juez legado) **ya fue mitigado**: hay un fixup
  idempotente en `Program.cs:57-74` que re-hashea a SHA-256 cualquier `Juez.Password` que no mida 44
  caracteres al arranque. El problema residual es el algoritmo (SHA-256 sin salt), no el texto plano.

Consecuencia práctica: el esfuerzo debe concentrarse en el código nuevo (que es el 100% de la superficie
HTTP), sin ignorar que el modelo legado sigue compartiendo la tabla `Jueces` con el sistema nuevo.

---

## 1. Resumen ejecutivo (≤10 líneas)

- **19 hallazgos**: **1 Crítico**, **6 Altos**, **7 Medios**, **5 Bajos** (incluye 2 informativos/verificados-limpios relevantes).
- **Bloquean producción (7):** SEC-01 (bypass de OAuth), SEC-02 (hashing sin salt), SEC-03 (admin semilla
  `abram/1234`), SEC-05 (cookie sin `Secure`), PRIV-01 (umbral de privacidad anulable a 0), CFG-01
  (entorno Development en runtime), CFG-02 (HTTPS/HSTS/forwarded headers para Azure).
- **Los 3 más urgentes:**
  1. **SEC-01 (Crítico)** — cualquier persona **sin autenticarse** puede crear una cuenta *activa* y quedar
     logueada haciendo un POST directo a `/Registro/CompletarGoogle`, sin pasar por Google. **Confirmado en
     vivo.** Permite squatting de matrículas/correos institucionales y, peor, **secuestrar el rol de Maestro
     Encargado** de un equipo (con su constancia).
  2. **SEC-02 (Alto)** — contraseñas con SHA-256 sin salt en `Registrante` y `Juez`: un volcado de BD se
     rompe con rainbow tables triviales.
  3. **SEC-03 (Alto)** — admin semilla `abram/1234` funcional (login confirmado en vivo), sin ruta forzada
     de cambio de contraseña.
- **Verificado limpio (no inflar):** sin SQL crudo (todo LINQ), sin `@Html.Raw`/XSS, índices únicos
  correctos, validación de `Identificador` sí es server-side, el umbral de privacidad sí se evalúa sobre el
  universo mostrado, `appsettings.json` gitignored y sin secretos, sin CVEs de runtime.

---

## 2. Tabla de hallazgos

Formato de cada campo tal como se pidió. Severidad calibrada a **riesgo real** de un sistema universitario
de alcance moderado, no a checklist genérico.

---

### SEC-01 — Bypass total de Google OAuth: creación de cuenta sin autenticación
- **Severidad:** 🔴 **Crítica**
- **Categoría:** Autenticación
- **Ubicación:** `Controllers/RegistroController.cs:251-313` (`CompletarGoogle` POST) y
  `:315-343` (`CompletarGoogleJuez` POST).
- **Riesgo/impacto:** Ambas acciones POST **crean una entidad persistida a partir de los campos posteados**,
  sin ninguna verificación de que el solicitante realmente completó un flujo de Google
  (`HttpContext.AuthenticateAsync("Google")`). El `email` viaja como campo oculto del formulario y el
  servidor confía en él ciegamente. Un atacante no autenticado puede:
  - Crear cuentas **activas** de participante (`ProveedorAuth="Google"`, `PasswordHash=null`, `Activo=true`)
    para cualquier correo `@uabc.edu.mx` y cualquier matrícula que no exista aún → **squatting/pre-emisión de
    identidad**: bloquea el registro legítimo de esa persona (índices únicos de correo/identificador) y
    genera cuentas basura a voluntad.
  - **Amplificación grave (impersonación de rol):** si el `Identificador` posteado coincide con el
    `MaestroEncargadoIdentificadorPendiente` de algún equipo, el código en `RegistroController.cs:296-309`
    **vincula automáticamente esa cuenta como Maestro Encargado del equipo y la promueve a `Maestro`** — el
    atacante queda como maestro responsable de un equipo ajeno y con derecho a su constancia.
  - Queda **auto-logueado** (se crea sesión con `CrearSesionRegistrante`), pudiendo operar el portal como esa
    identidad falsa.
  - `CompletarGoogleJuez` crea solicitudes de juez `Pendiente=true` (mitigado por la aprobación del admin,
    pero permite inundar la cola de aprobación y suplantar nombres/correos de jueces reales).
- **Evidencia (prueba dinámica ejecutada):** POST a `http://localhost:5185/Registro/CompletarGoogle` **sin
  cookies, sin sesión de Google y sin token antiforgery**, con
  `NombreCompleto=QA Audit Sentinel & CorreoInstitucional=qa-audit-sentinel@uabc.edu.mx & Identificador=90007`
  → respondió `HTTP 302 → /Registro/Dashboard` y creó la fila `Id=1019, ProveedorAuth=Google,
  PasswordHash=NULL, Activo=1` (conteo de `Registrantes` pasó de 3 a 4). Fila eliminada tras la prueba
  (conteo de vuelta a 3). La restricción de dominio `@uabc.edu.mx` **sí** se aplica (vía
  `Registrante.Validate`), así que el bypass está acotado a correos del dominio, pero eso no impide el
  squatting ni la toma de rol de maestro.
- **Recomendación:** No confiar en el `email` posteado. Reescribir el flujo para que estas acciones **exijan
  una autenticación de Google válida en curso**: al llegar al `GoogleCallback`, guardar el email/nombre
  verificados en un **cookie/token de un solo uso firmado y de vida corta** (o en un `AuthenticationProperties`
  cifrado / claim de un esquema externo temporal), y que `CompletarGoogle*` **lea el email de esa fuente
  server-side**, ignorando cualquier `CorreoInstitucional` que venga del formulario. Alternativamente, re-ejecutar
  `HttpContext.AuthenticateAsync("Google")` dentro del POST y tomar el email del principal. Añadir además
  `[ValidateAntiForgeryToken]` (ver SEC-10).
- **Esfuerzo:** **M**
- **¿Bloquea producción?** **Sí**

---

### SEC-02 — Hashing de contraseñas con SHA-256 sin salt
- **Severidad:** 🟠 **Alta**
- **Categoría:** Autenticación
- **Ubicación:** `RegistroController.cs:787-796` (`HashPassword`/`VerificarHash` de `Registrante`);
  `AccesoController.cs:42-51` (idéntico para `Juez`); fixup en `Program.cs:57-74`.
- **Riesgo/impacto:** `SHA256(UTF8(password))` en Base64, **sin salt ni stretching**. Ante un volcado de la
  tabla `Jueces`/`Registrantes` (backup filtrado, inyección futura, acceso a la BD de Azure), las
  contraseñas caen con rainbow tables/GPU en segundos. Los hashes idénticos revelan además contraseñas
  reutilizadas entre cuentas. Aplica **igual** a `Registrante` (participantes) y `Juez` (backoffice). Los
  participantes vía Google tienen `PasswordHash=null` (no afectados).
- **Evidencia (lectura de código):** confirmado el algoritmo idéntico en los tres puntos; el fixup de
  `Program.cs` solo re-hashea a **el mismo** SHA-256 (no mejora el algoritmo). No hay salt en ninguna parte
  del esquema (no existe columna de salt en `Juez`/`Registrante`).
- **Recomendación:** Migrar a **PBKDF2** (`Microsoft.AspNetCore.Identity.PasswordHasher<T>`, ya disponible sin
  dependencias nuevas) o **BCrypt/Argon2** (paquete `BCrypt.Net-Next` o `Isopoh.Cryptography.Argon2`). Estrategia
  de migración sin romper cuentas: en el próximo login exitoso, detectar hash viejo (44 chars Base64) y
  re-hashear con el algoritmo nuevo (rehash-on-login). Guardar un prefijo/formato que distinga el esquema.
  **Hacerlo antes de tocar los flujos de login** para no reescribir dos veces.
- **Esfuerzo:** **M**
- **¿Bloquea producción?** **Sí** (deuda ya reconocida en README; con datos reales de una universidad pública, es innegociable)

---

### SEC-03 — Cuenta administradora semilla `abram` / `1234` sin rotación forzada
- **Severidad:** 🟠 **Alta**
- **Categoría:** Autenticación
- **Ubicación:** `Models/SeedData.cs:24-32` (siembra `Usuario="abram", Password="1234", Rol="Admin"`).
- **Riesgo/impacto:** Credenciales por defecto, débiles y **documentadas públicamente en el README**. Dan
  acceso total al backoffice (gestión de jueces, registrantes, equipos, reportería con datos sensibles). No
  existe ninguna ruta que **fuerce** el cambio en el primer inicio de sesión; solo una nota en el README
  ("cámbialo cuanto antes"). El fixup de `Program.cs` la re-hashea al arranque, pero **la contraseña sigue
  siendo `1234`**.
- **Evidencia (prueba dinámica ejecutada):** `POST /Acceso/Login` con `usuario=abram&password=1234` →
  `HTTP 302 → /AltarEvaluacion/Historial` y sesión válida; con esa sesión, `GET /AltarAdmin/ReportePeriodo`
  y `/AltarAdmin/Jueces` → `HTTP 200`. La cuenta semilla es plenamente funcional como Admin.
- **Recomendación:** (a) Marcar la cuenta con un flag `DebeCambiarPassword` y forzar el cambio antes de
  permitir cualquier acción tras el primer login; **y/o** (b) no sembrar una contraseña fija: generarla
  aleatoria y escribirla al log de arranque una sola vez, o exigir que se defina por variable de entorno
  (`SEED_ADMIN_PASSWORD`) sin default. Como mínimo para producción: cambiarla y rotar. Considerar generar
  el `Usuario` semilla también configurable.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **Sí**

---

### SEC-05 — Cookie de sesión sin flag `Secure`; política de cookies no endurecida
- **Severidad:** 🟠 **Alta** (por ser el token de autenticación efectivo; fix trivial)
- **Categoría:** Autenticación / Configuración
- **Ubicación:** `Program.cs:15-18` (`AddSession` sin `Cookie.SecurePolicy`/`SameSite`/`HttpOnly`
  explícitos). La autenticación real del sistema se apoya en `HttpContext.Session` (`.AspNetCore.Session`),
  no en el esquema de cookies de auth (que solo se usa para el challenge de Google).
- **Riesgo/impacto:** La cookie `.AspNetCore.Session` **es** el portador de identidad (guarda `JuezId`,
  `JuezRol`, `RegistranteId`). Sin `Secure`, puede viajar por HTTP en un downgrade/red hostil y ser
  capturada → secuestro de sesión con el rol asociado (incluido Admin). `SameSite=Lax` (default) mitiga CSRF
  cross-site pero no protege contra sniffing en texto claro.
- **Evidencia (prueba dinámica ejecutada):** el `Set-Cookie` observado tras login fue
  `.AspNetCore.Session=...; path=/; samesite=lax; httponly` — **sin `secure`**. `HttpOnly` sí presente
  (bien: JS no la lee). `X-Frame-Options: SAMEORIGIN` presente. **Ausentes:** `Strict-Transport-Security`
  (solo en prod), `Content-Security-Policy`, `X-Content-Type-Options: nosniff`.
- **Recomendación:** En `AddSession`:
  `options.Cookie.SecurePolicy = CookieSecurePolicy.Always; options.Cookie.HttpOnly = true;
  options.Cookie.SameSite = SameSiteMode.Lax; options.Cookie.IsEssential = true;`. Añadir un middleware de
  cabeceras de seguridad (`X-Content-Type-Options: nosniff`, `Content-Security-Policy` básica,
  `Referrer-Policy`). Considerar `CookiePolicyOptions` global.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **Sí** (fix de una línea; no lanzar sin él)

---

### PRIV-01 — El umbral de agrupación de privacidad puede fijarse en 0/negativo y anular la protección de datos sensibles
- **Severidad:** 🟠 **Alta** (dato sensible bajo ley mexicana; control legal)
- **Categoría:** Privacidad
- **Ubicación:** `Models/Registro/ConfiguracionPeriodo.cs:44` (`UmbralAgrupacionDemografica`, `int`, sin piso);
  binding en `AltarAdminController.cs:270` (`config.UmbralAgrupacionDemografica = model.UmbralAgrupacionDemografica;`
  sin validar rango); `ViewModels/Altar/AdminViewModels.cs` (el campo no tiene `[Range]`);
  consumo en `Services/PrivacidadReporteHelper.cs:15-35` y `Services/ReportePeriodoService.cs:41,77,251-270`.
- **Riesgo/impacto:** La regla de `vision.md §12.2` (`N=5`) protege `Genero` y `AutodescripcionCultural`
  (este último equiparable a origen étnico/racial, **dato personal sensible**) de des-anonimización en cortes
  chicos. Si un admin fija el umbral en `0`, `AgruparConUmbralDePrivacidad` deja de agrupar nada
  (`Conteo >= 0` siempre verdadero, `reducidos` siempre 0) → **se muestran los conteos reales incluyendo
  categorías de 1 persona**, exponiendo a individuos identificables (p. ej. "1 persona autodescrita como X").
  El sistema **no lo impide ni lo advierte**. Con negativo, igual. No hay confirmación de doble paso.
- **Evidencia (lectura de código):** `PrivacidadReporteHelper.AgruparConUmbralDePrivacidad` usa `c.Conteo >= umbral`;
  con `umbral=0` no agrupa. `Configuracion` POST no valida piso. `ConfiguracionPeriodo` default es 5 (correcto),
  pero es editable a cualquier `int`. (No ejecuté la prueba dinámica de fijar 0 para no mutar la config real;
  la lógica es concluyente por lectura.)
- **Recomendación:** Imponer un **piso mínimo** (recomendado `>= 3`, idealmente `>= 5` como dice la visión)
  con `[Range(3, 100)]` en el ViewModel **y** una validación server-side en el POST de `Configuracion`
  (`AltarAdminController.cs`) que rechace valores por debajo del piso con `ModelState.AddModelError`. Añadir
  defensa en profundidad en `PrivacidadReporteHelper`/`ReportePeriodoService`: `var umbralEfectivo =
  Math.Max(umbral, PISO_MINIMO);`. Documentar en la UI de Configuración por qué no puede bajarse.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **Sí** (es el control técnico que respalda el cumplimiento de protección de datos)

---

### CFG-01 — Riesgo de desplegar con entorno `Development` (páginas de error con stack trace)
- **Severidad:** 🟠 **Alta** (si se materializa) / 🟡 Media (probabilidad)
- **Categoría:** Configuración / Logging
- **Ubicación:** `Program.cs:77-81` (`if (!app.Environment.IsDevelopment()) { UseExceptionHandler(...);
  UseHsts(); }`).
- **Riesgo/impacto:** En `Development`, ASP.NET Core muestra la **Developer Exception Page** con stack trace,
  rutas de archivos y, potencialmente, fragmentos de consulta — información valiosa para un atacante y con
  riesgo de filtrar datos. Si el App Service se despliega sin fijar `ASPNETCORE_ENVIRONMENT=Production`,
  se sirve en modo Development. Además, en Development **no** se aplican `UseExceptionHandler` ni `UseHsts`.
- **Evidencia (prueba dinámica ejecutada):** el arranque local reportó `Hosting environment: Development`.
  El manejo de errores de producción (`UseExceptionHandler("/Registro/Login")`) **sí** está bien resuelto
  para prod (redirige sin exponer traza) — el riesgo es puramente de **misconfiguración de entorno**.
- **Recomendación:** Fijar explícitamente `ASPNETCORE_ENVIRONMENT=Production` en la configuración del Azure
  App Service y documentarlo en el checklist de despliegue. Considerar una verificación de arranque que
  falle ruidosamente si detecta Development con una cadena de conexión no-local. Revisar que
  `UseExceptionHandler` cubra también respuestas JSON si se añaden APIs.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **Sí** (checklist de despliegue)

---

### CFG-02 — HTTPS/HSTS/forwarded headers no listos para Azure App Service
- **Severidad:** 🟡 **Media**
- **Categoría:** Configuración
- **Ubicación:** `Program.cs:83` (`UseHttpsRedirection` sin puerto HTTPS resoluble en el host),
  `:80` (`UseHsts` solo en no-Development), ausencia de `UseForwardedHeaders`.
- **Riesgo/impacto:** En Azure App Service el TLS se termina en el proxy de la plataforma; la app recibe
  HTTP por detrás. Sin `ForwardedHeaders` (`X-Forwarded-Proto`), `Request.IsHttps` es `false`, lo que rompe
  la emisión de cookies `Secure`, `UseHttpsRedirection` y la generación de URLs absolutas (callback de
  Google). El log local ya mostró `Failed to determine the https port for redirect`.
- **Evidencia (prueba dinámica + lectura):** advertencia `HttpsRedirectionMiddleware[3] Failed to determine
  the https port` en el arranque; no hay `app.UseForwardedHeaders(...)` en `Program.cs`.
- **Recomendación:** Añadir al inicio del pipeline `app.UseForwardedHeaders(new ForwardedHeadersOptions {
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });` (configurando
  KnownNetworks/Proxies o vaciándolos para App Service). Confirmar HSTS con `max-age` adecuado y considerar
  `includeSubDomains`. Verificar que el `RedirectUri` del callback de Google se genere como `https://` en
  prod.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **Sí** (para que las cookies `Secure` y el OAuth funcionen tras el proxy)

---

### SEC-10 — Ausencia de validación antiforgery (CSRF) en todos los POST
- **Severidad:** 🟡 **Media** (mitigado parcialmente por `SameSite=Lax`)
- **Categoría:** Inyección / CSRF
- **Ubicación:** Todos los controladores. Cero `[ValidateAntiForgeryToken]` / `[AutoValidateAntiforgeryToken]`
  en el proyecto. Ejemplos: `RegistroController` (Login, Signup, Ficha, AgregarIntegrante, QuitarIntegrante,
  DesignarNarrador, CompletarGoogle*), `AltarEvaluacionController.GuardarEvaluacion`, `AltarAdminController`
  (CrearJuez, AprobarJuez, DesactivarJuez, Desactivar/ReactivarRegistrante, Configuracion),
  `ConstanciaController.EnviarTodas`, `AccesoController.Login`.
- **Riesgo/impacto:** Un sitio malicioso podría inducir POSTs con la sesión de la víctima (aprobar un juez,
  cerrar una evaluación como Final, desactivar registrantes, cambiar la configuración/pesos/umbral, enviar
  constancias). **Mitigante real observado:** la cookie de sesión es `SameSite=Lax`, que **no se envía en
  POSTs cross-site**, lo que corta el vector clásico de formulario auto-enviado desde otro origen. El riesgo
  residual: navegadores viejos, ataques same-site (subdominios comprometidos), y cualquier acción de estado
  vía GET.
- **Evidencia (prueba dinámica ejecutada):** el POST a `/Registro/CompletarGoogle` **sin token antiforgery**
  fue aceptado y ejecutó la mutación (ver SEC-01) → confirma que el token **no se valida** aunque las vistas
  con `<form>` sí emiten la cookie `.AspNetCore.Antiforgery...` (observada, `samesite=strict; httponly`). Es
  decir: el token se genera pero nunca se verifica.
- **Recomendación:** Registrar el filtro global `builder.Services.AddControllersWithViews(o =>
  o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));` (valida todos los POST/PUT/DELETE
  automáticamente y respeta los `<form>` que ya emiten token). Verificar que los formularios que postean por
  JS (búsqueda de equipos, etc.) incluyan el token o sean GET. Confirmar que ningún cambio de estado se haga
  por GET.
- **Esfuerzo:** **M** (global es S; el M es por verificar cada form/JS)
- **¿Bloquea producción?** **No** (recomendado; defensa en profundidad sobre el `SameSite=Lax` ya presente)

---

### SEC-04 — Sin protección contra fuerza bruta / rate limiting en login
- **Severidad:** 🟡 **Media**
- **Categoría:** Autenticación
- **Ubicación:** `AccesoController.cs:19-40` (`Login` juez/admin), `RegistroController.cs:30-53`
  (`Login` participante), `RegistroController.cs:193-249` (callback Google). Sin lockout ni throttling.
- **Riesgo/impacto:** Un atacante puede probar contraseñas ilimitadamente contra `abram` u otros jueces/
  registrantes. Combinado con SHA-256 rápido (no aplica al online, pero sí a offline si hay volcado) y con la
  cuenta semilla conocida, el riesgo de toma de cuenta admin por fuerza bruta es real si no se rotó `1234`.
- **Evidencia (lectura de código + dinámica):** no hay contador de intentos, ni `Microsoft.AspNetCore.
  RateLimiting`, ni bloqueo temporal. Se pudieron enviar POSTs de login repetidos sin fricción.
- **Recomendación:** Añadir el middleware de **rate limiting** nativo de .NET 8
  (`builder.Services.AddRateLimiter(...)` con una política por IP para las rutas de login), y/o un contador
  de intentos fallidos por cuenta con backoff/lockout temporal. Emparejar con LOG-01 (registrar intentos).
- **Esfuerzo:** **M**
- **¿Bloquea producción?** **No** (fuerte recomendación; sube a Sí si no se resuelve SEC-03)

---

### LOG-01 — No se registran intentos de acceso no autorizado ni logins fallidos
- **Severidad:** 🟡 **Media**
- **Categoría:** Logging
- **Ubicación:** `AccesoController.Login`, `RegistroController.Login`, `AltarAdminController.RedirigirSinPermisos`
  (`:320-325`), `EstaAutenticado`/`EsAdmin` en todos los controladores.
- **Riesgo/impacto:** Sin registro de fallos de login ni de accesos denegados, es imposible detectar fuerza
  bruta, enumeración o abuso en curso (no hay señal para alertar ni para forense post-incidente).
- **Evidencia (lectura de código):** las rutas de fallo solo hacen `RedirectToAction`/`ModelState`; no hay
  `_logger.LogWarning` de seguridad. (Positivo: **no** se loguean contraseñas ni tokens; `EnableSensitiveData
  Logging` de EF **no** está activado, así que los parámetros SQL no exponen valores — verificado.)
- **Recomendación:** Inyectar `ILogger` en los controladores de auth y registrar, a nivel `Warning`, los
  logins fallidos (con usuario/correo intentado e IP, **sin** contraseña) y los accesos denegados en
  `RedirigirSinPermisos`. Enviar a Application Insights en Azure. No registrar datos sensibles.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### PRIV-05 — Registro local sin verificación de propiedad del correo
- **Severidad:** 🟡 **Media**
- **Categoría:** Autenticación / Privacidad
- **Ubicación:** `RegistroController.cs:66-133` (`Signup`), `:142-178` (`SignupJuez`).
- **Riesgo/impacto:** El registro local crea la cuenta sin confirmar que el solicitante controla el correo
  `@uabc.edu.mx`. Quien conozca la matrícula y el correo de otra persona (datos semi-públicos en una
  facultad) puede registrarla con una contraseña propia antes que ella (pre-emisión), o crear cuentas a
  nombre de terceros. Se solapa con SEC-01 pero por la vía local (con contraseña, por lo que el atacante sí
  puede loguearse). Para jueces queda mitigado por la aprobación admin.
- **Evidencia (lectura de código):** no hay envío de correo de verificación ni token de confirmación; la
  cuenta queda `Activo=true` y se crea sesión de inmediato.
- **Recomendación:** Añadir verificación de correo (enviar enlace/código con el `SmtpService` ya existente)
  antes de activar la cuenta, o al menos antes de permitir unirse a equipos. Alternativamente, para una
  universidad, **preferir Google OAuth como único registro** (ya restringido a `@uabc.edu.mx`) y limitar el
  registro local. Decisión de producto — documentar.
- **Esfuerzo:** **M**
- **¿Bloquea producción?** **No** (requiere confirmación humana sobre el modelo de registro deseado)

---

### SEC-11 — Regla "un registrante por equipo activo por periodo" solo a nivel de aplicación (TOCTOU)
- **Severidad:** 🟡 **Media** (probabilidad baja en este uso, impacto de integridad)
- **Categoría:** Concurrencia
- **Ubicación:** `RegistroController.cs:640-645` (`TieneEquipoActivoAsync` — check),
  `:397-470` (`CrearEquipo`), `:550-586` (`AgregarIntegrante`). No hay constraint de BD equivalente.
- **Riesgo/impacto:** El check-then-insert no es atómico. Dos requests simultáneos (crear equipo + ser
  agregado a otro, o dos "agregar" al mismo registrante) pueden **violar la regla** y dejar a una persona en
  dos equipos del mismo periodo, corrompiendo constancias/rankings. La clave primaria de `RegistranteEquipos`
  es `(RegistranteId, EquipoId)` — **impide duplicar en el mismo equipo, no across equipos del periodo**.
  (En contraste, la doble evaluación **sí** está protegida por índice único en `Evaluacion.EquipoId` —
  ver nota abajo.)
- **Evidencia (lectura de código):** `EvaluacionConfiguration.cs:24` tiene `HasIndex(EquipoId).IsUnique()`;
  `RegistranteEquipoConfiguration.cs:12` solo tiene la PK compuesta; no hay índice único que incluya el
  periodo. No ejecuté la carrera de condición en vivo (requeriría concurrencia controlada; el análisis es
  concluyente).
- **Recomendación:** Envolver el check+insert en una transacción con nivel de aislamiento serializable, o
  mejor, materializar el periodo en `RegistranteEquipo` y crear un **índice único filtrado**
  `(RegistranteId, Periodo)` que la BD imponga. Manejar la `DbUpdateException` resultante con un mensaje
  amable. Aplicar el mismo endurecimiento a `Evaluacion` (capturar la violación del índice único en
  `GuardarEvaluacion`, hoy lanzaría 500 en la carrera rara — ver SEC-15).
- **Esfuerzo:** **M**
- **¿Bloquea producción?** **No**

---

### SEC-08 — Autorización por chequeo manual en cada acción (sin filtro global) — frágil
- **Severidad:** 🟡 **Media** (defensa en profundidad; hoy **no** hay hueco explotable)
- **Categoría:** Autorización
- **Ubicación:** `EstaAutenticado()`/`EsAdmin()` repetidos en `AltarEvaluacionController`,
  `AltarAdminController`, `ConstanciaController`; sin `[Authorize]` ni middleware de autorización central.
- **Riesgo/impacto:** Cada acción nueva depende de que el desarrollador **recuerde** añadir el check. Un
  olvido = endpoint público. Es una bomba de tiempo de mantenimiento, no un hueco actual.
- **Evidencia (prueba dinámica ejecutada):** **todas** las rutas protegidas probadas sin sesión devolvieron
  `HTTP 302 → /Acceso` (admin: `ReportePeriodo`, `ReportePeriodoPdf`, `Jueces`, `RegistrantesYEquipos`;
  juez: `Historial`, `Detalle/1`; constancia: `DescargarGrupal/1`). O sea, **la cobertura actual es
  completa** — no encontré endpoint alcanzable sin sesión. El hallazgo es de robustez estructural.
- **Recomendación:** Migrar a autenticación por cookies con `ClaimsPrincipal` (rol como claim) y decorar los
  controladores con `[Authorize]`/`[Authorize(Roles="Admin")]`, o implementar un filtro/middleware de
  autorización que aplique por convención. Esto también habilita `User.IsInRole` y elimina la lógica de
  sesión manual.
- **Esfuerzo:** **L** (refactor de auth) — puede diferirse post-lanzamiento
- **¿Bloquea producción?** **No**

---

### PRIV-02 — El PDF del Reporte de Cierre no lleva marca de confidencialidad
- **Severidad:** 🔵 **Baja**
- **Categoría:** Privacidad
- **Ubicación:** `Services/ReportePeriodoService.cs:293-466` (`GenerarPdf`);
  `AltarAdminController.cs:288-295` (`ReportePeriodoPdf`). **(Pre-confirmado en `MEMORY.md`.)**
- **Riesgo/impacto:** El PDF agrega datos institucionales sensibles (participación por carrera, promedios por
  juez, demografía) pero se genera como documento plano, sin marca de agua ni pie "Confidencial — uso
  interno FIM/APFI". Una vez descargado y reenviado, nada indica su carácter reservado.
- **Evidencia (lectura de código + `MEMORY.md`):** el `GenerarPdf` no incluye watermark ni leyenda de
  confidencialidad; el pie solo pagina. Hallazgo **ya vetado por el usuario**, no re-investigado desde cero.
- **Recomendación:** Añadir en QuestPDF un texto de marca de agua diagonal ("CONFIDENCIAL") y/o una leyenda
  en header/footer con la fecha y el usuario que lo generó. Decisión de bajo esfuerzo, alto valor de señal.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### PRIV-03 — `ReportePeriodoPdf` sin rate-limiting propio (renderizado pesado)
- **Severidad:** 🔵 **Baja**
- **Categoría:** Privacidad / Rendimiento
- **Ubicación:** `AltarAdminController.cs:288-295`. **(Pre-confirmado en `MEMORY.md`.)**
- **Riesgo/impacto:** Cada request dispara agregación + render QuestPDF (costoso). Solo protegido por
  `EsAdmin()`, igual que el resto del backoffice. Un admin (o sesión admin secuestrada) podría abusarlo para
  DoS de CPU. **Recomendación de cierre:** dado que la protección es **consistente con el resto de
  `AltarAdminController`** y el actor debe ser admin autenticado, es **aceptable por paridad**; el único
  extra que amerita el contenido agregado es la marca de confidencialidad (PRIV-02), no un control de acceso
  distinto. Si se implementa el rate limiter global de SEC-04, incluir esta ruta en una política admin.
- **Evidencia:** `MEMORY.md` + lectura; el gate es idéntico al de las demás acciones admin.
- **Recomendación:** Cerrar como "aceptable por paridad"; opcionalmente cubrir con la política de rate
  limiting de SEC-04. No requiere cambio bloqueante.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### SEC-12 — Over-posting en `CrearJuez` (bind directo a la entidad EF)
- **Severidad:** 🔵 **Baja** (solo admin; campos críticos saneados)
- **Categoría:** Inyección / Mass assignment
- **Ubicación:** `AltarAdminController.cs:41-66` (`CrearJuez(Juez juez)` bindea la entidad `Juez` completa).
- **Riesgo/impacto:** El binder acepta cualquier propiedad de `Juez` posteada: `Id`, `IsDeleted`,
  `FechaEliminado`, `Pendiente`, `CorreoInstitucional`, `ProveedorAuth`. Mitigado porque solo un Admin llega
  aquí, `Rol` se sanea (`:50`) y `Pendiente`/`ProveedorAuth` se fijan explícitamente después. `Id`
  over-posteado podría chocar con la PK. Riesgo real bajo.
- **Evidencia (lectura de código):** es el **único** controlador que bindea una entidad EF directamente; el
  resto usa ViewModels/DTOs o parámetros primitivos (verificado en `RegistroController`,
  `AltarEvaluacionController.GuardarEvaluacion` usa parámetros sueltos + `Request.Form`, `Configuracion`
  usa `ConfiguracionPeriodoAdminViewModel`). El patrón general **sí** es seguro; esta es la excepción.
- **Recomendación:** Introducir un `CrearJuezViewModel` con solo los campos capturables (Usuario,
  NombreCompleto, Password, Rol) y mapear manualmente, como hace el resto del código.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### SEC-13 — Regla de negocio "un Narrador designado" no forzada en servidor al cerrar Final
- **Severidad:** 🔵 **Baja**
- **Categoría:** Lógica de negocio
- **Ubicación:** `AltarEvaluacionController.cs:226-378` (`GuardarEvaluacion`, rama `accion=="final"`);
  designación en `RegistroController.DesignarNarrador:617-638`.
- **Riesgo/impacto:** `vision.md §4.2` exige **un** Narrador por equipo. La designación se fuerza como
  auto-exclusiva en servidor (bien: `DesignarNarrador` pone Narrador a uno y el resto Integrante), pero al
  **cerrar una evaluación como Final no se valida que exista un Narrador designado** ni que la ficha esté
  completa. Se pueden cerrar evaluaciones (con constancias y ranking) de equipos que incumplen la regla.
  Integridad de datos, no seguridad.
- **Evidencia (lectura de código):** `GuardarEvaluacion` no consulta el rol de integrantes ni la completitud
  de ficha antes de `Estado=Final`. La auto-exclusividad del narrador **sí** está bien implementada.
- **Recomendación:** Antes de permitir `Final`, validar server-side: existe exactamente un `RolEquipo.Narrador`,
  ficha completa (`EsFichaCompleta`), maestro asignado. Rechazar con mensaje si no.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### SEC-14 — `RegistroController.Logout` no limpia toda la sesión
- **Severidad:** 🔵 **Baja**
- **Categoría:** Autenticación
- **Ubicación:** `RegistroController.cs:55-59` (`Logout` hace `Session.Remove("RegistranteId")`);
  contrasta con `AccesoController.Salir:59-63` que hace `Session.Clear()`.
- **Riesgo/impacto:** Si por algún flujo coexistieran claves de sesión de juez y registrante, el logout de
  participante dejaría vivas otras claves. Riesgo bajo en la práctica (los flujos no mezclan sesiones), pero
  es inconsistente y frágil.
- **Evidencia (lectura de código):** asimetría entre ambos logouts.
- **Recomendación:** Usar `HttpContext.Session.Clear()` en ambos logouts para invalidar toda la sesión.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### SEC-15 — `GuardarEvaluacion` no maneja la violación del índice único en carreras raras
- **Severidad:** 🔵 **Baja**
- **Categoría:** Concurrencia
- **Ubicación:** `AltarEvaluacionController.cs:255-280`.
- **Riesgo/impacto:** Dos jueces evaluando el mismo equipo simultáneamente: ambos pasan el check
  `equipo.Evaluacion == null` y ambos hacen `Add` → el segundo `SaveChangesAsync` viola el índice único
  `Evaluacion.EquipoId` y lanza `DbUpdateException` → **HTTP 500** al juez (no hay corrupción de datos, el
  índice protege la integridad, pero la UX es un error crudo). Probabilidad baja.
- **Evidencia (lectura de código):** el índice único existe (`EvaluacionConfiguration.cs:24`) pero
  `GuardarEvaluacion` no captura la excepción.
- **Recomendación:** Envolver en try/catch de `DbUpdateException` y mostrar "otro juez ya registró esta
  evaluación"; redirigir al detalle existente.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### DEP-01 — Vulnerabilidad Baja en dependencia transitiva `NuGet.Packaging/Protocol 6.11.0`
- **Severidad:** 🔵 **Baja / informativa**
- **Categoría:** Dependencias
- **Ubicación:** `AltarWeb.csproj:16` — llega **transitivamente** vía
  `Microsoft.VisualStudio.Web.CodeGeneration.Design` (scaffolding, **design-time**).
- **Riesgo/impacto:** `dotnet list package --vulnerable --include-transitive` reporta
  `NuGet.Packaging 6.11.0` y `NuGet.Protocol 6.11.0` con severidad **Low** (advisory GHSA-g4vj-cjjj-v7hg).
  Es una herramienta **de tiempo de diseño/scaffolding**, no se embarca en el runtime de producción → riesgo
  real de despliegue prácticamente nulo. EF Core 8.0.22, Authentication.Google 8.0.22 y QuestPDF 2025.7.4
  **sin CVEs**.
- **Evidencia (prueba ejecutada):** salida de `dotnet list package --vulnerable --include-transitive`
  (solo esos dos paquetes Low, ambos transitivos del paquete de scaffolding).
- **Recomendación:** Quitar `Microsoft.VisualStudio.Web.CodeGeneration.Design` del `.csproj` para el build de
  producción (no se necesita en runtime), o actualizarlo cuando haya versión que arrastre NuGet.* parcheado.
  Correr `dotnet list package --vulnerable` en el pipeline de CI como gate recurrente.
- **Esfuerzo:** **S**
- **¿Bloquea producción?** **No**

---

### E — Recomendaciones para cuando se habilite la subida real de archivos (RecorridoPdf) — aún no implementada
- **Severidad:** 🔵 **Informativa** (control preventivo, no hallazgo activo)
- **Categoría:** Archivos
- **Ubicación:** `ConfiguracionPeriodo.RecorridoPdf` (string), `AltarEvaluacionController.RecorridoPdf:380-404`,
  `Views/AltarAdmin/Configuracion.cshtml` (hoy solo muestra la ruta como texto en un `<input hidden>`).
- **Estado actual (verificado):** **no existe subida real** ni endpoint que sirva archivos por ruta. El
  `Configuracion` POST **ni siquiera setea** `RecorridoPdf` (queda como campo muerto). No hay path traversal
  hoy porque no se sirve ningún archivo desde disco por nombre controlado por el usuario.
- **Recomendación (antes de habilitar la subida real):** validar **tipo MIME real** (magic bytes, no solo
  extensión), tamaño máximo, nombre de archivo **generado por el servidor** (GUID, nunca el del usuario),
  almacenamiento **fuera de wwwroot** (idealmente Azure Blob Storage privado con SAS temporal), servir vía
  acción autenticada que valide rol (no enlace directo), y prevención explícita de path traversal
  (`Path.GetFileName`, rechazar `..`). Escaneo AV básico si el volumen lo amerita.
- **Esfuerzo:** **M** (cuando se implemente)
- **¿Bloquea producción?** **No** (la funcionalidad está diferida; documentar como requisito de esa feature)

---

## 3. Mapa de exposición de datos sensibles (`AutodescripcionCultural` / `Genero`)

Resumen para la sección C del encargo — dónde se leen/muestran/exportan:

| Superficie | ¿Muestra `AutodescripcionCultural`? | ¿Muestra `Genero`? | Sujeto al umbral §12.2 | Nota |
|---|---|---|---|---|
| `Registro/Signup`, `CompletarGoogle` (captura) | Sí (input propio) | Sí (input propio) | N/A | El propio titular captura su dato. OK. |
| `AltarAdmin/RegistrantesYEquipos` (gestión) | **No** (no se proyecta) | Sí (columna) | No aplica (gestión individual, permitido por §12.2) | Bien: `AutodescripcionCultural` **no** se expone en el listado admin — mínima exposición ya aplicada. `Genero` sí, aceptable para gestión. |
| `ReportePeriodo` / `ReportePeriodoPdf` (agregado) | Sí, agregado | Sí, agregado | **Sí** (vía `PrivacidadReporteHelper`) | Único corte demográfico; se agrupa bajo umbral. Ver PRIV-01 (umbral anulable). |
| Logs | No | No | — | Verificado: no se loguean datos personales; EF sin `EnableSensitiveDataLogging`. |
| Constancias PDF | No | No | — | Solo nombre/equipo/lugar. OK. |

**Confirmación §12.2 (positiva):** el umbral se evalúa sobre el **universo específico mostrado**
(`ConstruirEstadisticasDemograficas` agrupa sobre `registrantesEnPeriodo` y pasa por
`AgruparConUmbralDePrivacidad`). **No** existe una vista/exportación alternativa que muestre el detalle
demográfico por individuo saltándose el helper (el único desglose demográfico es el agregado del reporte;
no hay corte por-carrera de demografía que evada el umbral). La normalización de `AutodescripcionCultural`
es `Trim().ToLowerInvariant()` (temporal, `vision.md §13` pendiente de comité) — ver PRIV-04 abajo.

### PRIV-04 — Normalización temporal de `AutodescripcionCultural` (deuda documentada, no bug)
- **Severidad:** 🔵 Baja / seguimiento. **Categoría:** Privacidad.
- **Ubicación:** `ReportePeriodoService.cs:259-263`.
- **Nota:** `Trim().ToLowerInvariant()` puede **sub-agrupar** variantes ("migrante" vs "soy migrante") y dejar
  categorías chicas que, aunque el umbral las agrupe, reflejan conteos frágiles. Es una decisión **temporal
  reconocida** en `vision.md §13.12` (pendiente: manual vs. heurística). Sin acción de seguridad inmediata;
  cerrar con el comité. **No bloquea producción.**

---

## 4. Cobertura de la sección K (deuda ya identificada) — estado confirmado

1. **SHA-256 sin salt** → **Confirmado** en `Registrante`, `Juez` (y `Alumno` legado, sin superficie HTTP). → **SEC-02**.
2. **Placeholders de GoogleAuth** → **Confirmado seguro**: con placeholders, `AddGoogle` recibe strings no vacíos
   ("pendiente-configurar"); el `Challenge` de Google fallaría contra Google, pero **el login local sigue
   funcionando** (probado: `abram/1234` entra sin tocar Google) y **no hay fallback inseguro** — si faltara la
   config, `?? string.Empty` deja el proveedor sin credenciales y el botón de Google simplemente no autentica.
   Informativo, sin acción.
3. **Colisión de namespace `AltarWeb.Models.Registro` vs. legado** → **Sin colisión residual**: los controladores
   resuelven con alias explícitos (`using Equipo = AltarWeb.Models.Registro.Equipo;` etc.). El proyecto
   **compila con 0 warnings** (verificado). Cerrado.
4. **Dos rutas de auth (`/Acceso/Login` vs `/Registro/Login`)** → **Confirmado sin cruce de privilegios**: cada
   login consulta su propia tabla (`Jueces` vs `Registrantes`) y setea claves de sesión distintas
   (`JuezId`/`JuezRol` vs `RegistranteId`). Un `Registrante` no puede obtener `JuezRol` por la ruta de juez
   (no existe en `Jueces`) ni viceversa. **Sin embargo**, el riesgo real no es el cruce de rutas sino **SEC-01**
   (crear identidad sin auth). Cerrado el punto 4 como "sin cruce"; el foco es SEC-01.
5. **ReportePeriodo sin rate-limiting + PDF sin marca** → **Confirmados** (pre-vetados en `MEMORY.md`). →
   **PRIV-03** (aceptable por paridad, cerrar) y **PRIV-02** (añadir marca, bajo esfuerzo). No se
   re-investigaron desde cero.

---

## 5. Orden de implementación sugerido para Sonnet 5

Considerando dependencias entre hallazgos (arreglar cimientos antes que flujos que los usan):

**Fase 1 — Bloqueantes de producción (cimientos de auth y datos):**
1. **SEC-02** (hashing PBKDF2/BCrypt con rehash-on-login) — *primero*, porque reescribir después los flujos de
   login sería trabajo doble.
2. **SEC-01** (bypass OAuth: leer el email verificado server-side, no del POST) — el más grave; tocar junto con…
3. **SEC-10** (filtro global `AutoValidateAntiforgeryToken`) — barato y refuerza SEC-01 y todos los POST.
4. **SEC-03** (forzar rotación del admin semilla / password por env var).
5. **SEC-05 + CFG-01 + CFG-02** (bundle de despliegue: cookie `Secure`, `ASPNETCORE_ENVIRONMENT=Production`,
   `ForwardedHeaders`, HSTS, cabeceras de seguridad) — se tocan juntos en `Program.cs`.
6. **PRIV-01** (piso mínimo del umbral demográfico + validación) — control legal, esfuerzo S.

**Fase 2 — Endurecimiento fuerte (recomendado pre-lanzamiento):**
7. **SEC-04 + LOG-01** (rate limiting de login + logging de auth) — juntos, se refuerzan.
8. **PRIV-05** (verificación de correo o política de solo-Google) — requiere decisión de producto.
9. **SEC-11 + SEC-15** (constraint de unicidad por periodo + manejo de `DbUpdateException`) — concurrencia, juntos.
10. **PRIV-02** (marca de confidencialidad en el PDF).

**Fase 3 — Deuda de robustez (post-lanzamiento aceptable):**
11. **SEC-08** (refactor a `[Authorize]`/claims) — mayor esfuerzo, sin hueco actual.
12. **SEC-12** (DTO para `CrearJuez`), **SEC-13** (validar narrador/ficha antes de Final),
    **SEC-14** (`Session.Clear()` en ambos logouts), **DEP-01** (quitar scaffolding del build de prod),
    **PRIV-03** (cerrar por paridad), **PRIV-04** (normalización con el comité).
13. **E** (validaciones de subida de archivos) — solo cuando se implemente la subida real del recorrido PDF.

---

## 6. Higiene de la sesión de auditoría (pruebas dinámicas)

- Se levantó la app en `localhost:5185` (LocalDB), se ejecutaron las pruebas descritas en las secciones de
  Evidencia, y se **detuvo la app** al terminar.
- Único dato mutado: **una** fila QA sentinela (`Identificador=90007`, `qa-audit-sentinel@uabc.edu.mx`),
  **eliminada** al terminar. Conteo de `Registrantes` verificado antes (3) y después (3). **No se tocó ni se
  volcó ningún dato real** de registrantes (los correos/matrículas reales nunca se materializaron, por
  política de manejo de PII).
- Pruebas **no ejecutadas** por ser mutantes/riesgosas, documentadas para referencia: fijar el umbral en 0 en
  la config real (concluyente por lectura), condición de carrera de membresía (requiere concurrencia
  controlada), fuerza bruta real de login, y cualquier flujo que requiera credenciales reales de Google/SMTP.
