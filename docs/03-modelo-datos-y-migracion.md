# 03 — Modelo de datos y migración

> Fecha: 2026-10-17. Decisiones de fondo en [ADR-001 §2.6, §7.1 y §12](01-decision-arquitectura.md).

## 1. Dos bases, sin nada compartido

| | Altares | Catrinas |
|---|---|---|
| Contexto EF | `AltarDbContext` (`src/Altares.Web/Models`) | `CatrinasDbContext` (`src/Catrinas.Web/Datos`) |
| Cadena de conexión | `ConnectionStrings:AltarContext` | `ConnectionStrings:CatrinasContext` |
| Migraciones | `src/Altares.Web/Migrations` (historial completo desde 2025-12) | `src/Catrinas.Web/Datos/Migraciones` (`InicialCatrinas`) |
| Se aplican | al arrancar (`SeedData.Initialize` → `Database.Migrate()`) | al arrancar (`SemillaCatrinas.Inicializar` → `Database.Migrate()`) |

Ninguna app tiene la cadena de conexión de la otra ni consulta su base. Una cuenta existe por
separado en cada plataforma (misma identidad de Google, dos registros independientes; ADR §2.4).

## 2. Altares

Esquema vigente sin cambios destructivos durante la separación:
- **F2:** Catrina sale de la UI y de la lógica de Altares. Las tablas y columnas de Catrina
  (`EvaluacionesCatrina`, `Equipo.HaceCatrina`, pesos de Catrina en `ConfiguracionesPeriodo`) se
  **conservan como legado** sin uso. Su eliminación irá en una migración posterior y separada,
  con respaldo previo (ADR §12).
- **F2:** `EquiposConcurso.CodigoPublico` (`ALT-{Periodo}-{NNNN}`), único e inmutable. Migración
  `20261017183635_AgregarCodigoPublicoAEquipos`: agrega la columna, rellena los equipos
  existentes por periodo en orden de `Id`, crea `ContadoresCodigoPublico` y el índice único. El
  consecutivo se asigna con `MERGE … WITH (HOLDLOCK)` dentro de la transacción de alta del equipo.
- **F4:** el recálculo de lugares (1.º–3.º por carrera y periodo) se serializa con un bloqueo de
  aplicación de SQL Server (`Concurso.Core.Datos.BloqueoExclusivo`), sin cambio de esquema.

## 3. Catrinas

Modelo de ADR §7.1, en la migración `InicialCatrinas`:
- `Registrantes`, `Jueces` (jurado propio), `Carreras`, `Generos`, `ConfiguracionesPeriodo`.
- `Inscripciones` con `MiembrosInscripcion` (`Integrante` o `Personificador`), responsable,
  maestro encargado opcional y la **referencia blanda** a un altar: `AltarExternalId`
  (`ALT-…`, validado solo por formato) + `AltarNombreSnapshot`, **sin llave foránea** a Altares.
- `Evaluaciones` (una por inscripción) con `rowversion` para concurrencia optimista, *snapshot*
  de pesos y datos, e `IntegrantesEvaluados`.
- Restricciones en la base: CHECK de rangos (criterios 0–10, pesos que suman 1, umbral ≥ 3),
  índices únicos (nombre por periodo, una persona por inscripción por periodo, un solo
  Personificador por inscripción, una evaluación por inscripción).
- **F4:** el recálculo de lugares se serializa por carrera y periodo y escribe con
  `ExecuteUpdate`, de modo que dos cierres Final simultáneos ya no chocan por `rowversion`.

## 4. Migración a producción

No hay datos que migrar entre plataformas:
- Catrinas empieza vacía (no hay histórico de Catrina; decisión del propietario).
- Altares en producción también empieza vacía en la base nueva del hosting. Si se quisiera
  conservar el histórico de una base anterior, se restaura su `.bak` en la base de Altares
  **antes** del primer arranque y la app aplica solo las migraciones pendientes (F2 está probada
  sobre una base existente con datos).

Probado en F4 (2026-10-17): ambas apps, **desde el paquete publicado**, migran y siembran una
base vacía al primer arranque.

## 5. Datos demo, datos de carga y datos reales

**Regla:** los datos demo y los de la prueba de carga nunca conviven con datos reales. Antes de
cerrar el gate de privacidad (ADR §8.4) las bases del hosting solo tienen datos ficticios; antes
de abrir el registro real se limpian y se verifica que quedan en cero.

| Conjunto | Crear | Limpiar | Marca para reconocerlo |
|---|---|---|---|
| Demo Altares | `datos-demo.sql` (raíz) | `limpiar-datos-demo.sql` (raíz) | correos `*.demo@uabc.edu.mx`; 4 equipos con nombres fijos |
| Demo Catrinas | `src/Catrinas.Web/Datos/Demo/datos-demo-catrinas.sql` | `…/limpiar-datos-demo-catrinas.sql` | correos `*.demo@uabc.edu.mx`; inscripciones `… (demo)` |
| Prueba de carga (ambas) | `tools/PruebaCarga` escenario `preparar` | `tools/PruebaCarga/limpiar-carga-{altares,catrinas}.sql` | correos `carga.*@uabc.edu.mx`; equipos/inscripciones `Carga NNN`; jueces `carga-juez-N` |

**Cómo correr un script** contra la base del hosting: desde el administrador SQL del panel de
MonsterASP (pegar el contenido) o con `sqlcmd -I -f 65001 -S <servidor> -d <base> -i <archivo>`
si se habilitó el acceso remoto a la base. `-I` es obligatorio en Catrinas (índices filtrados) y
`-f 65001` conserva los acentos.

**Verificar que producción está limpia** (justo antes de abrir el registro):
`despliegue/verificar-limpieza-altares.sql` y `despliegue/verificar-limpieza-catrinas.sql`. Solo
leen; **todas las filas deben dar 0**. El script de Altares muestra además el siguiente
`CodigoPublico` de cada periodo: los scripts de limpieza reinician el consecutivo cuando el
periodo queda sin equipos, para que el primer equipo real sea `…-0001`.

Probado en F4 sobre bases locales: crear demo → limpiar → verificar (0); preparar carga
(Altares: 70 equipos, 210 cuentas, 20 jueces; Catrinas: 100 inscripciones, 200 cuentas, 20 jueces),
evaluar → limpiar → verificar (0).
