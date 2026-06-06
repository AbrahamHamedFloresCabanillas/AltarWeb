# AltarWeb - Estado Actual del Proyecto

> Actualizado: 2026-06-06

## Stack Tecnologico
| Capa | Tecnologia |
|---|---|
| Framework | ASP.NET Core MVC 8.0 |
| ORM | Entity Framework Core + SQL Server / LocalDB |
| PDF | QuestPDF Community |
| Frontend | Bootstrap 5 + FontAwesome 6 + JS vanilla |
| Email | SMTP configurable en `Smtp:*` |
| Auth | Sistema propio por sesiones + Google OAuth para alumnos |

## Modelos Principales

### `Juez`
`Id, NombreCompleto, Usuario, Password, Rol, IsDeleted, FechaEliminado`

### `Alumno`
`Id, NombreCompleto, Matricula, CorreoElectronico, PasswordHash, ProveedorAuth, Activo, FechaRegistro, PeriodoRegistro`

### `Equipo`
`Id, NombreEquipo, PeriodoAcademico, CreadoPorAlumnoId, SnapshotNombreCreador, Activo, FechaCreacion`

### `AlumnoEquipo`
Tabla puente `Alumno <-> Equipo` con `FechaIngreso` y `EsCreador`.

### `Integrante`
Se mantiene para historial de evaluaciones antiguas creadas antes del portal de alumnos. El nuevo flujo usa `Alumno`.

### `Evaluacion`
Ahora soporta `EquipoId`, `SnapshotNombreEquipo`, `NotaTradicionFinal` y `NotaPersonalizacionFinal`, conservando `NombreEquipo` para historial/snapshot.

## Funcionalidades Completadas

- [x] Login/Logout para Jueces y Admins existente conservado.
- [x] Modelo `Alumno` con registro local y Google OAuth.
- [x] Validacion de matricula numerica y unica global.
- [x] Validacion de correo unico `@uabc.edu.mx` para alumnos.
- [x] Sesion de alumno separada (`AlumnoId`, `AlumnoNombre`, `AlumnoMatricula`).
- [x] Portal `/Alumno/Login`, `/Alumno/Registro`, `/Alumno/Dashboard`.
- [x] Google OAuth configurado con placeholders en `appsettings.json`.
- [x] Modelo `Equipo` y relacion many-to-many con `AlumnoEquipo`.
- [x] Un alumno solo puede pertenecer a un equipo por periodo academico.
- [x] Creacion de equipo por alumno con busqueda de integrantes.
- [x] Vista `/Alumno/MiEquipo`.
- [x] Edicion de integrantes por el creador del equipo mientras el equipo no tenga evaluacion registrada.
- [x] Vista `/Alumno/MiEvaluacion` con estados sin equipo, pendiente y evaluado.
- [x] Descarga de constancia individual propia con validacion de pertenencia.
- [x] Evaluacion refactorizada para seleccionar equipo con dropdown en tiempo real.
- [x] Endpoints JSON `/Evaluacion/BuscarEquipos` y `/Evaluacion/ObtenerIntegrantesEquipo`.
- [x] Constancias grupal e individual centralizadas en `ConstanciaService`.
- [x] Descarga de constancia grupal y ZIP de individuales.
- [x] Envio de constancias por correo desde servicio configurable.
- [x] Panel admin `/Admin/Alumnos` para buscar, desactivar y reactivar alumnos.
- [x] Panel admin `/Admin/Equipos` y `/Admin/EquiposHistorico` para listar/reactivar/desactivar equipos.
- [x] Migracion `SprintAlumnosEquiposConstancias` creada y aplicada.

## Dependencias NuGet

- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools`
- `Microsoft.AspNetCore.Authentication.Google`
- `Microsoft.VisualStudio.Web.CodeGeneration.Design`
- `QuestPDF`

## Decisiones de Arquitectura

- Se mantiene `Integrante` para datos historicos de evaluaciones antiguas.
- El flujo nuevo usa `Alumno` como entidad autenticable y `AlumnoEquipo` como membresia.
- La membresia del equipo queda editable por su creador solo hasta que exista una `Evaluacion` asociada; despues se conserva cerrada para historial y constancias.
- `appsettings.json` usa placeholders para Google OAuth y SMTP; los secretos reales deben ir en user-secrets, variables de entorno o configuracion local no versionada.

## Deuda Tecnica

- `AlumnoEquipo` no tiene soft delete propio. La edicion admin de integrantes actualiza la membresia de la tabla puente; si se requiere trazabilidad completa de membresias removidas, agregar `Activo` a `AlumnoEquipo` en una migracion posterior.
- El login de jueces sigue usando el campo `Password` existente en texto plano. No se cambio para evitar romper cuentas actuales; conviene migrarlo a hash en un sprint dedicado.
- Las vistas nuevas son funcionales, pero pueden recibir una segunda pasada visual para pulir detalles del tema.

## Historial de Sesiones

| Fecha | Agente | Cambios Realizados |
|---|---|---|
| 2026-06-06 | Codex | Creador de equipo puede agregar o quitar integrantes desde `/Alumno/MiEquipo` solo antes de que el equipo sea evaluado; documentacion y reglas actualizadas. |
| 2026-06-05 | Codex | Portal de alumnos, Google OAuth, equipos por periodo, seleccion de equipo en evaluaciones, constancias grupales/individuales, admin de alumnos/equipos, migracion aplicada y documentacion actualizada. |
