# 02 — Plan de separación Altares / Catrinas (estado)

> Fecha: 2026-10-17. La decisión y sus razones están en [ADR-001](01-decision-arquitectura.md); este
> documento sigue la ejecución. Rama: `separacion-altares-catrinas` (sin push). Punto de retorno:
> tag `pre-separacion` (`6c800ae`).

## 1. Fases

Cada fase cerró con build, todas las pruebas, revisión del diff, revisión de seguridad si tocaba
autenticación, documentación y commit independiente, y esperó la autorización del propietario.

| Fase | Contenido | Estado |
|---|---|---|
| F0A | Suite mínima de pruebas y baseline en .NET 8 | Hecha |
| F0B | .NET 8 → .NET 10 (EF Core / ASP.NET Core 10.0.13) | Hecha |
| F1 | Monorepo `src/`+`tests/`; `Concurso.Core` transversal; SEC-04 por cuenta + techo por IP; SEC-01 con `email_verified`/`hd`; ForwardedHeaders restringido (CFG-02) | Hecha |
| F2 | Altares sin Catrina (esquema conservado); `CodigoPublico` `ALT-{Periodo}-{NNNN}` | Hecha |
| F3 | `Catrinas.Web` independiente (modelo de ADR §7.1), datos demo, M2 corregido | Hecha |
| F4 | Despliegue, gates R6/R7, correo, carga, independencia, aceptación, documentación | **En curso**: todo lo que no depende del hosting está hecho; ver [07](07-checklist-produccion.md) |

## 2. Commits de la rama

| Fase | Commits |
|---|---|
| F0A / docs ADR | `9173f4c`, `6741506`, `02a46e8`, `d6fa21f` |
| F0B | `2713d15`, `fbeca71` |
| F1 | `14aea99`, `f735239`, `4539f9c`, `93f3f42`, `969c088`, `4d72c13` |
| F2 | `43ebb9e`, `90bf4a3`, `e64fd33` |
| F3 | `e8e8df7`, `785081a`, `ab89a38` |
| F4 | `a169859` (F4a: M2 en Altares, mitigación de M1), `ea4072a` (F4b: diagnóstico solo admin), F4c (defectos hallados por la prueba de carga y en el correo), F4d (herramienta de carga, empaquetado y scripts de despliegue), F4e (documentación final) |

Los hashes de F4c–F4e se listan en el reporte de cierre y en `git log`.

## 3. Pruebas

| Suite | Pruebas (2026-10-17, tras F4) |
|---|---:|
| `Concurso.Core.Tests` | 54 |
| `Altares.Tests` | 76 |
| `Catrinas.Tests` | 68 |
| **Total** | **198**, 0 omitidas, 0 fallidas |

```bash
dotnet test AltarWeb.sln -c Release
```

Las pruebas de integración usan SQL Server LocalDB (una base desechable por fábrica) y no
envían correo (SMTP apuntado a un puerto local cerrado).

## 4. Qué falta y quién lo hace

1. **Propietario:** contratar y configurar la infraestructura (04 §A), variables de entorno,
   subir paquetes ([07 §1](07-checklist-produccion.md)).
2. **Juntos, en el hosting:** R6, R7, correo real, carga, independencia, aceptación manual,
   prueba de restauración ([07 §2–5](07-checklist-produccion.md)).
3. **Propietario/UABC:** gate de privacidad (ADR §8.4) antes de cargar datos reales.
4. Limpieza de datos ficticios y verificación en cero; apertura del registro.

**Fallback de calendario** (ADR §9): si el hosting no estuviera listo a tiempo, se despliega solo
Altares separado y Catrinas opera con `pre-separacion`; requiere aceptación del propietario.
