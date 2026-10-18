# PruebaCarga — prueba de carga de Altares y Catrinas

Consola .NET 10 sin dependencias externas. Simula navegadores reales (cookies propias, token
antiforgery, sin seguir redirecciones) contra **una** app por ejecución y reporta, por operación:
total, 5xx, timeouts, 429, otros 4xx, p50/p95/p99/máx. En `mixto` y `pdf` muestrea cada 3 s la
memoria del proceso desde el endpoint de diagnóstico (solo admin) y detecta reinicios.

**Solo contra bases de staging o con datos ficticios. Nunca contra producción con datos reales.**

## Credenciales

Solo por variables de entorno del proceso que corre la herramienta (nunca en argumentos ni
archivos versionados):

| Variable | Qué es |
|---|---|
| `PRUEBA_ADMIN_USUARIO` / `PRUEBA_ADMIN_PASSWORD` | Un admin de la app bajo prueba (con la contraseña temporal ya cambiada) |
| `PRUEBA_CARGA_PASSWORD` | Contraseña (≥ 8) que tendrán las cuentas ficticias `carga.*` y `carga-juez-*` |

## Escenarios

```powershell
dotnet run -c Release --project tools/PruebaCarga -- --url https://altares.<dominio> --app altares --escenario <escenario> [opciones]
```

| Escenario | Qué hace |
|---|---|
| `preparar` | Crea `--equipos` equipos/inscripciones ficticios completos (Altares: organizador, integrante, maestro y Narrador; Catrinas: responsable, integrante y Personificador), `--jueces` jueces y cierra como Final los primeros `--reservados-pdf` para el escenario `pdf`. |
| `login` | `--usuarios` inicios de sesión de cuentas distintas, repartidos en `--duracion` s, todos desde la misma IP (simula el NAT de la FIM). |
| `mixto` | `--usuarios` simultáneos durante `--duracion` s: participantes (dashboard, ver y guardar ficha), jueces (búsqueda, Preliminar repetido y Final en el último cuarto) y 2 admins (historial, listados, ranking/reporte). Rampa de `--rampa` s. |
| `pdf` | `--pdf-concurrencia` sesiones descargando constancias (grupal, ZIP de individuales, juez) y, en Altares, el Reporte de Cierre. |
| `abuso` | Cuenta atacada (8 fallidos), vecino de NAT con la misma IP y `--usuarios` intentos contra cuentas distintas para verificar el techo por IP. Deja la IP limitada un minuto y la cuenta atacada cinco. |

Opciones: `--equipos` (70; usar 100 en Catrinas para 200 cuentas), `--jueces` (20),
`--reservados-pdf` (10), `--usuarios` (200), `--duracion` (300), `--rampa` (30),
`--pdf-concurrencia` (20), `--salida <archivo.md>`, `--certificado-local` (acepta el certificado de
desarrollo **solo** en `localhost`).

Orden recomendado: `preparar` → `login` → (esperar 1 min) → `mixto` → `pdf` → `abuso` (al final,
porque bloquea la IP un minuto).

## Limpieza

`limpiar-carga-altares.sql` y `limpiar-carga-catrinas.sql` borran solo lo creado por `preparar`
(y reinician el consecutivo de `CodigoPublico` si el periodo queda vacío). Después,
`despliegue/verificar-limpieza-*.sql` debe dar 0. Ver [docs/03 §5](../../docs/03-modelo-datos-y-migracion.md).

Resultados de la línea base local: [docs/07 §3](../../docs/07-checklist-produccion.md).
