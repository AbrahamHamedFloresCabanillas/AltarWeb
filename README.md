# Concursos de Día de Muertos — FIM UABC

Monorepo de las dos plataformas del concurso de la Facultad de Ingeniería (FIM) de la UABC. Desde
octubre de 2026 son **dos aplicaciones independientes**, cada una con su propio sitio, base de
datos, cuentas, cliente de Google y buzón de correo:

| Plataforma | Código | Qué hace | README |
|---|---|---|---|
| **Altares** | `src/Altares.Web` | Registro de equipos y Ficha del altar, evaluación Preliminar/Final, ranking por carrera, constancias, Reporte de Cierre | [src/Altares.Web/README.md](src/Altares.Web/README.md) |
| **Catrinas** | `src/Catrinas.Web` | Inscripción (ligada a un altar o independiente), Personificador, jurado propio, evaluación, ranking, constancias | [src/Catrinas.Web/README.md](src/Catrinas.Web/README.md) |
| Infraestructura común | `src/Concurso.Core` | Solo lo transversal: contraseñas, sesión, rate limiting, Google institucional, correo, PDF base, privacidad, diagnóstico. **Sin reglas de negocio.** | — |

Stack: ASP.NET Core MVC **.NET 10**, EF Core 10 + SQL Server, QuestPDF.

## Estructura

```
AltarWeb.sln
src/      Altares.Web · Catrinas.Web · Concurso.Core
tests/    Altares.Tests · Catrinas.Tests · Concurso.Core.Tests
tools/    PruebaCarga (prueba de carga) · Publicar (paquetes de despliegue)
despliegue/  portada del dominio raíz · scripts de verificación de datos limpios
docs/     documentación de la separación, despliegue y operación (abajo)
```

## Empezar en local

Requisitos: .NET 10 SDK y SQL Server LocalDB.

```bash
dotnet build AltarWeb.sln
dotnet test AltarWeb.sln
```

Para correr cada app, crea su `appsettings.json` a partir de su `appsettings.Ejemplo.json` (ninguno
de los dos `appsettings.json` se versiona) y sigue su README.

## Documentación

| Documento | Para qué |
|---|---|
| [docs/00-auditoria-estado-actual.md](docs/00-auditoria-estado-actual.md) | De dónde se partió y dónde estamos |
| [docs/01-decision-arquitectura.md](docs/01-decision-arquitectura.md) | ADR-001: por qué y cómo se separó (fuente de verdad) |
| [docs/02-plan-separacion-altares-catrinas.md](docs/02-plan-separacion-altares-catrinas.md) | Fases, commits y pruebas |
| [docs/03-modelo-datos-y-migracion.md](docs/03-modelo-datos-y-migracion.md) | Bases, migraciones, datos demo y verificación de datos limpios |
| [docs/04-despliegue-altares.md](docs/04-despliegue-altares.md) | Infraestructura común + despliegue de Altares |
| [docs/05-despliegue-catrinas.md](docs/05-despliegue-catrinas.md) | Despliegue de Catrinas |
| [docs/06-costos-infraestructura.md](docs/06-costos-infraestructura.md) | Costos |
| [docs/07-checklist-produccion.md](docs/07-checklist-produccion.md) | Checklist de salida, resultados de carga, gates |
| [docs/08-rollback-y-recuperacion.md](docs/08-rollback-y-recuperacion.md) | Rollback, respaldos y restauración |
| [docs/09-operacion-y-mantenimiento.md](docs/09-operacion-y-mantenimiento.md) | Operación diaria y pendientes post-concurso |
| [vision.md](vision.md) | Especificación funcional del comité |
| [current_state.md](current_state.md) | Estado técnico interno |
| [AUDITORIA_SEGURIDAD.md](AUDITORIA_SEGURIDAD.md) | Auditoría de seguridad y estado de cada hallazgo |
| [DESPLIEGUE.md](DESPLIEGUE.md) | **Obsoleto** (despliegue de la app única anterior) |

## Seguridad y secretos

Ningún secreto se versiona: los `appsettings.json` locales están en `.gitignore` y en producción la
configuración vive en las variables de entorno de cada sitio. Los paquetes de despliegue no llevan
ningún `appsettings*.json` (lo verifica `tools/Publicar/publicar.ps1`).
