# 08 — Rollback y recuperación

> Fecha: 2026-10-17. Complementa [ADR-001 §12](01-decision-arquitectura.md#12-datos-migración-y-rollback).

## 1. Puntos de retorno

| Punto | Qué es | Cómo se usa |
|---|---|---|
| Tag `pre-separacion` (`6c800ae`) | La app única (.NET 8) antes de la separación | Rollback total (sección 4). Verificado el 2026-10-17: el tag se extrae en un *worktree* y compila. |
| Un commit por fase en `separacion-altares-catrinas` | F0A, F0B, F1a–d, F2, F3, F4 | `git revert <commit>` de la fase problemática (ver [02 §2](02-plan-separacion-altares-catrinas.md)) |
| ZIP de cada despliegue (`<App>-<commit>-<runtime>.zip`) | Paquete exacto que se subió | Conservar **el anterior** de cada app para volver en minutos (sección 2) |
| Backup diario del hosting | Copia de cada base | Restauración (sección 3) |
| Backup manual antes de cada migración o de abrir el registro | Copia en un momento conocido | Restauración (sección 3) |

## 2. Volver al despliegue anterior de UNA app (sin tocar la otra)

1. Sube `app_offline.htm` al sitio afectado.
2. Sube el contenido del ZIP **anterior** de esa app (sin borrar `App_Data/` ni `wwwroot/uploads/`).
3. Borra `app_offline.htm`.

Si el despliegue que se revierte **agregó una migración**, la app anterior no la conoce, pero
mientras la migración sea aditiva (columnas nulas o tablas nuevas) sigue funcionando. Si no lo es,
restaura además la base (sección 3) al respaldo previo a esa migración. Por eso: **respaldo manual
antes de cada despliegue que traiga migraciones**.

## 3. Respaldo y restauración de una base (MonsterASP)

**Respaldo manual:** *Databases → (base) → Backups → Create backup*. Nombrarlo con fecha y motivo
(p. ej. `antes-de-abrir-registro`). Descargar una copia `.bak` a un lugar seguro y privado (contiene
datos personales una vez abierto el registro).

**Restauración:** *Databases → (base) → Backups* → elegir un respaldo del panel o **Upload Backup**
con un `.bak` → *Restore*. Antes de restaurar, detén la app con `app_offline.htm` y vuelve a
arrancarla después.

**Prueba de restauración (obligatoria antes del concurso, con datos ficticios):**
1. Con la base de Altares con datos demo, crea un respaldo manual.
2. Corre `limpiar-datos-demo.sql` y comprueba con `despliegue/verificar-limpieza-altares.sql` que
   quedó en 0.
3. Restaura el respaldo del paso 1 y comprueba que los datos demo volvieron (equipos visibles en
   la app, constancia descargable).
4. Repite con Catrinas.
5. Anota fecha y resultado en [07](07-checklist-produccion.md) (punto 5).

## 4. Rollback total a la app única (`pre-separacion`)

Solo si la separación fallara de forma irrecuperable antes del concurso.

**Atención — base de datos:** la app de `pre-separacion` **no conoce** la columna
`EquiposConcurso.CodigoPublico` (F2), que es `NOT NULL` y sin valor por defecto. Si esa app corre
sobre una base que ya tiene esa migración, **crear equipos falla**. Antes de volver hay que:
- restaurar un respaldo de la base de Altares anterior a F2, **o**
- revertir solo esa migración (su `Down` borra la tabla de contadores, el índice y la columna):
  `dotnet ef database update 20260911205005_ValidacionAdminDeFichaDeRegistro --project src/Altares.Web --connection "<cadena>"`
  desde una máquina con acceso remoto a la base.

Además:
- `pre-separacion` es **.NET 8** (fin de soporte: 2026-11-10); el sitio debe tener el runtime 8.
- Catrinas vuelve a vivir dentro de Altares; los datos capturados en la base de Catrinas **no** se
  migran solos.
- Se publica con `dotnet publish AltarWeb/AltarWeb.csproj -c Release -r win-x86 --self-contained false`
  desde el tag, y se configura como describe el `DESPLIEGUE.md` **de ese tag**.

## 5. Recuperación ante incidentes comunes

| Síntoma | Causa probable | Acción |
|---|---|---|
| `HTTP 500.30` al arrancar | Arquitectura del paquete distinta a la del app pool, o falta una variable obligatoria | Ver el log (04 §B.8). Si dice que falta `GoogleAuth:ClientId`, define la variable. Si es arquitectura, republica con el `-Runtime` correcto. |
| Todos pierden la sesión tras un despliegue | Se borró `App_Data/keys` | Nada que restaurar: vuelven a iniciar sesión. No borrar esa carpeta en el siguiente despliegue. |
| Muchos 429 legítimos | Techo por IP bajo para el tráfico real, o R6 (todo llega con la IP de un proxy) | Revisar Diagnóstico; subir `Seguridad__RateLimiting__IpLoginPorMinuto` (nunca el límite por cuenta) y reiniciar. |
| No llegan correos | SMTP o reputación | Las constancias siguen descargables. Revisar SPF/DKIM; pasar a Gmail como *fallback* (04 §B.7). |
| Cuenta de admin bloqueada (5 fallidos) | Límite por cuenta | Esperar 5 minutos; otra cuenta admin puede seguir trabajando. |
| Admin perdió la contraseña | — | **Altares:** otro admin la restablece en *Jueces → Editar* (queda con cambio obligatorio). **Catrinas:** no hay restablecimiento en la UI; otro admin crea una cuenta nueva y desactiva la anterior. Sin otro admin en ninguna de las dos: ver [09 §4](09-operacion-y-mantenimiento.md). |
