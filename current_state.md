# AltarWeb - Estado Actual del Proyecto

> **Actualizado 2026-10-17 (F4, rama `separacion-altares-catrinas`).** El repositorio ya son **dos
> plataformas independientes**: `src/Altares.Web` (lo que describe este documento) y `src/Catrinas.Web`
> (ver su [README](src/Catrinas.Web/README.md)), con infraestructura común en `src/Concurso.Core`,
> pruebas en `tests/` (198 en verde) y herramientas en `tools/`. Las secciones de abajo sobre
> controladores, modelo y deuda siguen describiendo Altares; lo que cambió con la separación está
> resumido en "Estado tras la separación". Estado general y pendientes de salida a producción:
> [`docs/00-auditoria-estado-actual.md`](docs/00-auditoria-estado-actual.md) y
> [`docs/07-checklist-produccion.md`](docs/07-checklist-produccion.md).

Este documento describe el **estado técnico interno** del sistema (qué existe hoy en el código).
Los otros documentos del repo cubren lo demás:

| Documento | Responde a |
|---|---|
| [`vision.md`](vision.md) (v2.6) | **Qué** debe construirse — especificación funcional, fuente de verdad del comité. |
| [`README.md`](README.md) | Índice del monorepo; cada app tiene su README en `src/<App>/`. |
| [`docs/04`](docs/04-despliegue-altares.md) y [`docs/05`](docs/05-despliegue-catrinas.md) | Despliegue vigente (`DESPLIEGUE.md` es obsoleto). |
| [`AUDITORIA_SEGURIDAD.md`](AUDITORIA_SEGURIDAD.md) | Auditoría de seguridad (22 hallazgos) y **estado de remediación** de cada uno. |
| Este archivo | Estado técnico: arquitectura, modelo de datos, deuda técnica, historial. |

---

## Estado tras la separación (2026-10-17)

- **Altares** ya no tiene Catrina en la UI ni en la lógica (tablas legadas conservadas, sin uso) y
  cada equipo tiene un `CodigoPublico` inmutable `ALT-{Periodo}-{NNNN}`.
- **Catrinas** es una app aparte con base, cuentas, jurado, cookies, Data Protection, cliente OAuth y
  buzón propios.
- **Seguridad añadida:** límite de login por cuenta + techo por IP; Google exige `email_verified` y
  `hd`; revalidación de sesión de cuentas desactivadas (M2); tiempo de login igual para cuentas
  inexistentes; diagnóstico de red y memoria solo para admin.
- **Robustez añadida en F4:** recálculo de lugares serializado; errores y *timeout* de SMTP
  manejados; la app no arranca sin credenciales de Google; GC de estación de trabajo.
- **Despliegue:** paquetes `win-x86` sin configuración, secretos en variables de entorno del sitio.
- Las secciones siguientes conservan la descripción de Altares de 2026-10-10; donde dicen
  `AltarWeb/`, hoy es `src/Altares.Web/`.

## Estado general

El sistema está **funcionalmente completo** respecto al alcance de `vision.md` v2.6: se
re-implementó sobre el modelo `Registro` (Fase 2), se eliminaron las rutas legadas, se corrió una
auditoría de seguridad completa con sus tres fases de corrección, y después cuatro rondas de
ajustes de UI/UX, fórmulas, responsividad y tema claro/oscuro/sistema.

Lo que queda abierto son **decisiones de comité** (no implementación pendiente) y la deuda técnica
listada más abajo.

---

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Framework | ASP.NET Core MVC (.NET 10, desde 2026-10-17) |
| ORM | Entity Framework Core + SQL Server / LocalDB |
| PDF | QuestPDF 2025.7.4 (licencia comunitaria) |
| Frontend | Sistema de diseño propio "Noche de Altar" (CSS3) + FontAwesome 6 + JS vanilla |
| Email | SMTP configurable en `Smtp:*` |
| Auth | Sesión propia (`ISession`) + Google OAuth (`@uabc.edu.mx`) para participantes y jueces |
| Hash de contraseñas | PBKDF2 vía `Microsoft.AspNetCore.Identity.PasswordHasher` |

`AltarWeb/appsettings.json` **no se versiona** (está en `.gitignore`): un clon limpio debe recrearlo
con la plantilla del README antes de poder ejecutar. Lo mismo aplica a `AltarWeb/wwwroot/uploads/`.

---

## Arquitectura

### Controladores (5 — son el 100% de la superficie HTTP)

| Controlador | Responsabilidad |
|---|---|
| `AccesoController` | Login/logout real de jueces y admins, cambio de contraseña obligatorio. |
| `RegistroController` | Portal de Registro: landing, signup local y Google (participante y juez), completar datos de Google, dashboard, creación de equipo, Ficha de Registro, gestión de integrantes. |
| `AltarEvaluacionController` | Nueva evaluación, historial, detalle, avance por carrera, recorrido en PDF. |
| `AltarAdminController` | Jueces (alta/edición/aprobación/soft-delete), registrantes (edición/soft-delete) y equipos (edición completa de la Ficha e integrantes, validación administrativa), configuración del periodo, Reporte de Cierre de Periodo. |
| `ConstanciaController` | Generación y envío de constancias (grupal, individuales ZIP, maestro, juez, propia). |

No existen `JuecesController` ni `AlumnoController`: las rutas legadas se eliminaron por completo.

### Servicios y filtros

- `ConstanciaService` — generación (QuestPDF) y envío por correo de todas las constancias.
- `EvaluacionCalculoService` — puntaje de elementos, nota final y cálculo de lugar por carrera (la nota y el ranking de Catrina se retiraron en F2, 2026-10-17).
- `ReportePeriodoService` — Reporte de Cierre de Periodo (vista y PDF, con marca de confidencialidad).
- `PrivacidadReporteHelper` — regla de umbral de agrupación demográfica (`vision.md` §12.2), con piso mínimo no anulable.
- `PasswordHashService` — PBKDF2 + reconocimiento y migración de hashes SHA-256 legados (rehash-on-login).
- `EquipoValidacionHelper` — regla única del estado de un equipo: `EsFichaCompleta`, `FaltantesFicha` (qué falta, para poder decirlo), `TieneNarradorDesignado`, `FueValidadaPorAdmin` y `EstaListoParaFinal` (narrador + ficha completa, con el motivo del rechazo). Es la **única** fuente: el portal del participante, el panel admin, Nueva Evaluación y el Reporte de Cierre la consumen, ya no hay copias.
- `MaestroEncargadoHelper` — resuelve el identificador de maestro encargado capturado en la Ficha (vincula si existe como Maestro/Administrativo y lo promueve a Maestro; lo deja pendiente si no existe; rechaza alumnos). Era un método privado de `RegistroController`; se extrajo porque `AltarAdminController.EditarEquipo` usa la misma regla.
- `FechaConcursoHelper` — "hoy" según el calendario de Mexicali (`America/Tijuana`) y la comparación de fechas límite. Existe porque las fechas límite son fechas de calendario y compararlas contra `DateTime.Now` las hacía depender de la zona horaria del hosting (Europa en MonsterASP, UTC en Azure).
- `ForzarCambioPasswordFilter` — bloquea toda acción de una cuenta con `DebeCambiarPassword=true`.
- Filtro global `AutoValidateAntiforgeryTokenAttribute` (CSRF en todos los POST/PUT/DELETE).

---

## Modelo de datos

### Vigente — namespace `AltarWeb.Models.Registro`

| Entidad | Notas |
|---|---|
| `Registrante` | Alumno (7 dígitos) / Maestro / Administrativo (5 dígitos); correo institucional, teléfono, género, carrera, autodescripción cultural, soft delete. |
| `Equipo` | Nombre del grupo y del altar, carrera del altar, periodo, organizador, maestro encargado (o identificador pendiente), ubicación, `CodigoPublico` inmutable (`ALT-{periodo}-{NNNN}`, F2), `HaceCatrina` (**legado sin uso desde F2**), y la validación administrativa de la ficha (`FichaValidadaPorAdminEn`, `FichaValidadaPorJuezId`). |
| `RegistranteEquipo` | Membresía con `Rol` (Integrante \| Narrador) y `Periodo` denormalizado para el índice único. |
| `Difunto` | Nombre, fecha de defunción, semblanza/temática, tipo de altar (Tradicional \| Niños). |
| `Elemento` / `ElementoEvaluado` | Catálogo maestro de 24 elementos (mini-manual: significado y colocación) y su escala de satisfacción por evaluación (**No lo tiene / Regular / Bueno**, persistida como texto). |
| `Evaluacion` / `EvaluacionIntegrante` | Estado `Preliminar` \| `Final`, componentes 0–10, nota final, lugar, snapshots de equipo/integrantes/difunto. |
| `EvaluacionCatrina` | **Legado sin uso desde F2 (2026-10-17):** tabla histórica que se conserva hasta después del concurso; Altares ya no la lee ni la escribe. |
| `ContadorCodigoPublico` | Último consecutivo de `CodigoPublico` por periodo (F2); solo lo incrementa `CodigoPublicoEquipo` dentro de la transacción del equipo. |
| `ConfiguracionPeriodo` | Fechas límite, recorrido PDF, pesos de nota final (los de Catrina quedan como columnas legadas sin uso desde F2), escala de satisfacción (3 valores), pesos ritual/decorativo, bonus temático, umbral demográfico. |
| `CatalogoGenero` / `CatalogoCarrera` | Catálogos editables desde el admin (13 carreras FIM). |

### Legado durmiente

`Juez` sigue vigente (backoffice). `Alumno`, `AlumnoEquipo`, `Integrante` y la `Evaluacion` antigua
permanecen en el `DbContext` **sin ningún controlador ni ruta HTTP que los exponga**: son datos
históricos, deuda de esquema y no superficie de ataque.

---

## Funcionalidades completadas

**Portal de Registro (participantes)**
- [x] Signup local y Google OAuth restringido a `@uabc.edu.mx`, con `CompletarGoogle` para los datos que Google no entrega.
- [x] Validación de identificador por tipo (7 dígitos alumno / 5 dígitos maestro y administrativo), server-side.
- [x] Dashboard: estado del equipo y de la Ficha, integrantes, maestro encargado, catálogo de elementos requeridos.
- [x] Creación de equipo y **Ficha de Registro del Altar** completa, editable hasta la primera evaluación o la fecha límite de requisitos.
- [x] Un registrante por equipo activo por periodo, forzado también por índice único en BD.
- [x] Designación de narrador y cierre de la lista de integrantes al existir evaluación.
- [x] Descarga de constancia propia cuando la evaluación del equipo está en `Final`.

**Jueces y evaluación**
- [x] Auto-registro de juez (local o Google) con aprobación del administrador; login bloqueado mientras esté pendiente.
- [x] Búsqueda de equipo por carrera en tiempo real y precarga en solo lectura de la Ficha.
- [x] Checklist de 24 elementos agrupado por nivel (3 o 7), con escala de satisfacción de 3 niveles, marcador de tematizado, mini-manual (i) y acumulado en vivo.
- [x] Categorías 0–10: Objetivo Cultural (sugerido desde el puntaje de elementos), Creatividad, Valoración General, Distribución por Niveles, Explicación oral.
- [~] ~~Catrina como categoría independiente y opcional, con ranking propio por carrera.~~ Retirada de Altares en F2 (2026-10-17); pasa a `Catrinas.Web`.
- [x] Estados `Preliminar` (reeditable) y `Final` (confirmación explícita; exige narrador y ficha completa).
- [x] Historial por periodo, avance por carrera (Final / En preliminar / Sin calificar) y recorrido en PDF embebido.

**Administración**
- [x] Gestión de jueces: alta manual, **edición** (`/AltarAdmin/EditarJuez/{id}`: nombre, usuario, rol Juez↔Admin, contraseña nueva opcional que fuerza cambio en el siguiente login; las cuentas Google no exponen usuario ni contraseña; un admin no puede quitarse su propio rol), aprobación de pendientes, soft delete y reactivación.
- [x] Gestión de registrantes: listado, **edición** (`/AltarAdmin/EditarRegistrante/{id}`: nombre, tipo, identificador, correo, teléfono, género, carrera, autodescripción; sin contraseña; unicidad contra activos e inactivos; vincula equipos que esperaban ese identificador como maestro encargado, igual que el signup; no permite pasar a Alumno a quien ya es maestro encargado), soft delete y reactivación.
- [x] Gestión de equipos: listado del periodo y **edición completa de la Ficha** (`/AltarAdmin/EditarEquipo/{id}`, `vision.md` §6.2): nombre, altar, difunto, carrera, maestro encargado, lugar (Catrina retirada en F2), más agregar/quitar integrantes y designar Narrador. El admin **no está sujeto a las fechas límite**; con evaluación registrada puede corregir la ficha (el snapshot de la evaluación no se reescribe) pero la lista de integrantes y el narrador quedan cerrados. El organizador no puede quitarse desde ahí.
- [x] Validación administrativa de la Ficha de Registro (`vision.md` §6.3), desde el listado de equipos y desde Nueva Evaluación.
- [x] Configuración por periodo: fechas límite, subida del recorrido PDF, pesos de nota final, umbral demográfico (pesos de Catrina retirados en F2).
- [x] Reporte de Cierre de Periodo (`vision.md` §12) con umbral de agrupación demográfica y exportación a PDF marcado como confidencial.

**Constancias**
- [x] Grupal, individuales en ZIP, maestro encargado, juez y descarga propia del participante.
- [x] Envío manual por correo desde el detalle de la evaluación; se generan para todos, sin umbral de nota.

**Transversal**
- [x] Tema claro / oscuro / sistema con persistencia en `localStorage` y sin parpadeo al recargar.
- [x] Diseño responsivo y animaciones/micro-interacciones del sistema de diseño.
- [x] Auditoría de seguridad aplicada en 3 fases (ver `AUDITORIA_SEGURIDAD.md`).

---

## Deuda técnica (vigente)

1. **CSP con `script-src 'unsafe-inline'`** (`Program.cs:134-152`) — el layout y varias vistas tienen `<script>` inline (tema, sidebar, datepicker). Reduce el valor de la CSP contra XSS. Cerrarlo exige mover esos scripts a archivos estáticos o usar nonces por request.
2. **Autorización por chequeo manual de sesión** (SEC-08) — la cobertura actual es completa, pero depende de que cada acción nueva recuerde llamar `EstaAutenticado()`/`EsAdmin()`. Migrar a `[Authorize]`/`ClaimsPrincipal` es robustez, no un hueco activo.
3. **Recorrido PDF servido como archivo estático público** — se guarda en `wwwroot/uploads/recorridos/recorrido-{periodo}.pdf` con validación de extensión, firma `%PDF` y límite de 25 MB, pero quien conozca la URL puede abrirlo sin sesión. Servirlo desde una acción autenticada (o Blob privado con SAS) sigue pendiente.
4. ~~Rate limiting de login particionado por IP (10/min)~~ — **resuelto 2026-10-17**: límite por cuenta + techo amplio por IP (`AUDITORIA_SEGURIDAD.md` §0-ter). Falta calibrar el techo por IP con la prueba de carga.
5. **`RegistranteEquipo` sin soft delete propio** — quitar un integrante borra la membresía. Si se requiere trazabilidad de membresías removidas, agregar `Activo` en una migración posterior.
6. **Modelos legados en el `DbContext`** (`Alumno`, `AlumnoEquipo`, `Integrante`, `Evaluacion` antigua) — sin rutas que los expongan; conviene decidir si se archivan sus datos y se retiran del esquema.
7. **Regla de empates no implementada** — `vision.md` §13, pendiente 11: el ranking por carrera no define criterio de desempate. No hay nada en código al respecto.
8. **Nombres internos desalineados con la UI** — el cambio de etiquetas de 2026-09-11 se aplicó **solo a los textos visibles**. En código y BD siguen llamándose `EsenciaPersonalidad` (UI: *Creatividad*) y `Narrador`/`PesoNarrador` (UI: *Explicación oral*). Renombrarlos tocaría 17 archivos más migraciones de columnas sin ningún beneficio para el usuario, así que se decidió **no hacerlo**; quien lea el código debe tener presente el mapeo. El enum `Satisfaccion` sí se renombró (`NoLoTiene`/`Regular`/`Bueno`) porque sus valores se persisten como texto y quedaban ilegibles.
9. **Restricción de nivel y repetición de elementos sin implementar** — `vision.md` §13, pendientes 13–15 (elementos que solo pueden ir en cierto nivel y su penalización, pregunta de "¿está presente más de una vez?", y "los niveles son elementos"). Están **bloqueados por definición del comité**, no por esfuerzo técnico: hoy `NivelSugerido3/7` es solo sugerencia y reasignar el nivel no afecta la nota.
10. ~~Sin proyecto de tests~~ — **resuelto 2026-10-17** (`tests/Altares.Tests`, `tests/Concurso.Core.Tests`). Texto original: **Sin proyecto de tests** — la solución solo contiene `AltarWeb.csproj`. Las reglas de `EquipoValidacionHelper` y `FechaConcursoHelper` son funciones puras y serían el primer candidato natural a cubrir con xUnit; hoy se verifican a mano contra el ensamblado compilado.

---

## Estado de la Ficha de Registro (cómo se decide)

No existe una columna "ficha pendiente": el estado se **deriva**. Los tres conceptos viven
separados a propósito y no deben mezclarse (`vision.md` §6.3):

| Concepto | Origen | Efecto |
|---|---|---|
| **Ficha completa** | `EquipoValidacionHelper.EsFichaCompleta` — nombre del altar, nombre del difunto, lugar de exposición y maestro encargado **vinculado**. | Requisito para cerrar `Final`. |
| **Ficha validada** | `Equipo.FichaValidadaPorAdminEn != null`, lo pone un administrador. | Solo seguimiento: deja de listarse como pendiente. |
| **Listo para Final** | `EquipoValidacionHelper.EstaListoParaFinal` — ficha completa **+** exactamente un Narrador. | Habilita el cierre como `Final`. |

Validar **no** relaja las otras dos: si faltan datos, la UI muestra "validada" y el faltante a la vez.

## Fechas límite (cómo se comparan)

Regla única en `FechaConcursoHelper.LimitePasado`: el día configurado **cuenta completo** (el
bloqueo empieza al día siguiente), `null` significa "sin límite", y la comparación se hace contra
el calendario de **Mexicali** (`America/Tijuana`, que sí observa horario de verano), nunca contra
la zona horaria del servidor.

| Fecha | Bloquea | No bloquea |
|---|---|---|
| `FechaLimiteInscripcion` | Crear equipos nuevos (GET y POST de `CrearEquipo`). | El registro de participantes, ni los equipos ya creados. |
| `FechaLimiteRequisitos` | Editar la Ficha, agregar/quitar integrantes y designar Narrador. | La evaluación del juez ni las acciones del administrador. |

Independientemente de la fecha, un equipo **con evaluación registrada** (Preliminar o Final) tiene
la ficha y los integrantes cerrados, para que coincidan con lo que el juez evaluó.

## Decisiones de producto pendientes (no técnicas)

- **Verificación de correo en registro local** (PRIV-05): riesgo **aceptado explícitamente** por el propietario el 2026-07-04, sin cambios de código. Pendiente de revisión futura.
- **Normalización de `AutodescripcionCultural`** (PRIV-04 / `vision.md` §13.12): hoy `Trim().ToLowerInvariant()` como solución temporal. El comité debe elegir entre catálogo manual de equivalencias o heurística.
- **Valores de la escala de satisfacción y pesos ritual/decorativo** (`vision.md` §13.10): los **nombres** de los tres niveles quedaron confirmados el 2026-09-11 (*No lo tiene / Regular / Bueno*); los **valores** `0 / 0.5 / 1.0` y los pesos ritual/decorativo siguen pendientes de confirmación, configurables por periodo.
- **Regla de empates en el ranking por carrera** (`vision.md` §13.11): sin definir.
- **Elementos restringidos a un nivel, repetición de elementos y "los niveles son elementos"** (`vision.md` §13.13–15): pedidos el 2026-09-11 pero **sin la definición necesaria para implementarlos** (falta el mapeo elemento→nivel obligatorio y la consecuencia sobre la nota).

---

## Historial de sesiones

| Fecha | Agente | Cambios realizados |
|---|---|---|
| 2026-06-05 | Codex | Portal de alumnos, Google OAuth, equipos por periodo, selección de equipo en evaluaciones, constancias grupales/individuales, admin de alumnos/equipos, migración aplicada. |
| 2026-06-06 | Codex | El creador del equipo puede agregar o quitar integrantes desde `/Alumno/MiEquipo` solo antes de que el equipo sea evaluado. |
| 2026-07-01/02 | Claude | Fase 1 y 2 de `vision.md`: modelo `Registro`, Portal de Registro, paneles de juez y admin, eliminación de rutas legadas. |
| 2026-07-03 | Opus 4.8 | Auditoría de seguridad, privacidad y preparación para producción: 19 hallazgos, sin tocar código. |
| 2026-07-04 | Claude | Fases 1–3 de remediación de la auditoría, Reporte de Cierre de Periodo, fechas límite e índices únicos de concurrencia. |
| 2026-08-06 | Claude | Reconfiguración de criterios de Catrina, precarga del catálogo de elementos y ajustes de UX. |
| 2026-08-27 | Claude | Animaciones y micro-interacciones, corrección de validación en cliente, listado de jueces inactivos, limpieza de código muerto y alineación de documentación. |
| 2026-09-02 | Claude | Actualización de este documento y anotación del estado de remediación en `AUDITORIA_SEGURIDAD.md`; guía `DESPLIEGUE.md` y scripts de datos de demostración. |
| 2026-09-05 | Claude | Corrección del callback de Google (ruta propia `/Registro/google-listo` y lectura del ticket desde el esquema de cookie), marcado de campos obligatorios en los formularios de registro y revisión de documentación. |
| 2026-09-11 | Opus 5 | Escala de satisfacción de 4 a 3 niveles (*No lo tiene / Regular / Bueno*) con migración de datos `EscalaSatisfaccionTresNiveles`; renombres de UI *Personalizado*→*Tematizado*, *Esencia y Personalidad*→*Creatividad*, *Narrador*→*Explicación oral* (el rol de equipo sigue siendo *Narrador*); `vision.md` a v2.6 con tres pendientes nuevos. |
| 2026-09-11 | Opus 5 | Ronda de UX y lógica administrativa: datepicker rehecho y movido a `wwwroot/js/altar-datepicker.js`; arreglo de contraste del toggle en tema claro; validación administrativa de la Ficha de Registro (migración `ValidacionAdminDeFichaDeRegistro`); auditoría de fechas límite (huecos server-side en integrantes/narrador y comparación dependiente de la zona horaria del servidor); unificación de `EsFichaCompleta`, que tenía una copia divergente en el panel admin. |
| 2026-09-13 | Opus 5 | Edición administrativa de jueces, registrantes y equipos (`EditarJuez`, `EditarRegistrante`, `EditarEquipo` + `AgregarIntegranteEquipo` / `QuitarIntegranteEquipo` / `DesignarNarradorEquipo`), botones de edición en los tres listados y extracción de `MaestroEncargadoHelper`. Sin migraciones. Commit `c615f9a`. |
| 2026-10-10 | Opus 5.5 | Alineación de documentación con el código: rutas reales en `vision.md`, número de migraciones en `DESPLIEGUE.md`, conteos de la auditoría y orden de este historial. Sin cambios de código. |
