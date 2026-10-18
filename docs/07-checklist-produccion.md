# 07 — Checklist de producción (cierre de F4)

> Estado al 2026-10-17. **Producción NO está declarada lista**: faltan los pasos que dependen del
> hosting real y de acciones del propietario. Cada casilla dice quién la cierra y con qué evidencia.
> Leyenda: ✅ hecho y verificado · 🟡 hecho en local, falta repetirlo en el hosting · ⬜ pendiente.

## 1. Infraestructura (propietario, guiado por 04 §A)

| # | Paso | Estado |
|---|---|---|
| 1.1 | Dominio comprado | ⬜ |
| 1.2 | MonsterASP Premium Single contratado; datacenter elegido y anotado | ⬜ |
| 1.3 | DNS (opción A o B) propagado: `<dominio>`, `www`, `altares`, `catrinas` resuelven | ⬜ |
| 1.4 | Tres sitios creados (raíz, `altares`, `catrinas`), .NET 10, InProcess; arquitectura del app pool anotada | ⬜ |
| 1.5 | HTTPS Let's Encrypt activo y con renovación automática en los tres | ⬜ |
| 1.6 | Dos bases MSSQL creadas; backups diarios confirmados en el panel | ⬜ |
| 1.7 | Dos clientes OAuth con sus orígenes y callbacks exactos (04 §A.6) | ⬜ |
| 1.8 | Buzones `altares@` y `catrinas@`; DKIM activo; SPF y DMARC publicados | ⬜ |
| 1.9 | Variables de entorno de cada sitio (04 §B.1, 05 §2) | ⬜ |
| 1.10 | Paquetes subidos; admin semilla entró, cambió contraseña; `SEED_ADMIN_PASSWORD` borrada | ⬜ |

## 2. Cierre de F4 (puntos 1–20 pedidos por el propietario)

| # | Punto | Estado y evidencia |
|---|---|---|
| 1 | URLs finales | ⬜ Estructura decidida: `https://altares.<dominio>`, `https://catrinas.<dominio>`, portada en `https://<dominio>`. Falta el dominio. |
| 2 | Hosting elegido | 🟡 MonsterASP Premium Single (ADR §8.2); plan B Azure B1. Falta contratar. |
| 3 | Costos reales | ⬜ Estimado ~34.48 USD / ~635 MXN el primer año ([06](06-costos-infraestructura.md)); llenar con la factura. |
| 4 | Bases creadas | 🟡 Migración desde base vacía probada con el paquete publicado (ambas apps). Faltan las bases del hosting. |
| 5 | Backups | ⬜ Confirmar respaldo diario y hacer la restauración de prueba ([08 §3](08-rollback-y-recuperacion.md)). |
| 6 | R6 (cabeceras reenviadas) | ⬜ Abierto. Herramienta lista: `/AltarAdmin/Diagnostico` y `/Admin/Diagnostico`; procedimiento en 04 §B.5. No se cierra por que HTTPS funcione. |
| 7 | R7 (Google `hd`) | ⬜ Abierto. Procedimiento en 04 §B.6. Si una cuenta UABC válida no trae `hd`, se detiene y se decide el *fallback* (sección 4 de este documento). |
| 8 | Correo probado | 🟡 Errores SMTP y *timeout* corregidos y cubiertos por pruebas (`CorreoSmtpTests`, `CorreoConstanciasTests`). Falta envío real con SPF/DKIM (04 §B.7). |
| 9 | Resultados de carga | 🟡 Línea base local completa (sección 3). Falta repetir `mixto` y `pdf` contra el hosting y medir la RAM real. |
| 10 | Rate limits finales | 🟡 Por cuenta: 5 fallidos / 5 min (sin cambios, no se reduce). Por IP: 300 logins/min y 600 OAuth/min **se mantienen** (sección 3.3). Revisar con los 429 del hosting. |
| 11 | Aceptación Altares | 🟡 Flujo completo cubierto por pruebas automáticas y por la carga (registro, equipo, ficha, integrantes, Narrador, Preliminar, Final, ranking, constancias, reporte, admin). Falta el recorrido manual en el hosting, incluido Google. |
| 12 | Aceptación Catrinas | 🟡 Igual (registro, inscripción, integrantes, Personificador, referencia `ALT-*`, Preliminar, Final con confirmación, ranking, constancias, admin). Falta el recorrido manual en el hosting, incluido Google. |
| 13 | Independencia | 🟡 Probada en código y pruebas (bases, cookies, Data Protection, despliegues y paquetes separados). Falta la prueba en el hosting (sección 5). |
| 14 | Security review final | 🟡 Hecha sobre el código de F4 ([AUDITORIA_SEGURIDAD.md §0-quater](../AUDITORIA_SEGURIDAD.md)). Se repite tras R6/R7. |
| 15 | Vulnerabilidades de dependencias | ✅ `dotnet list package --vulnerable --include-transitive`: sin vulnerabilidades en lo que se publica. Solo `NuGet.Packaging/Protocol 6.12.1` (Low), traídos por el *scaffolding* de Debug, que no entra al paquete. |
| 16 | Total de pruebas | ✅ Ver [02 §3](02-plan-separacion-altares-catrinas.md). |
| 17 | Documentación final | ✅ `docs/00–09`, README raíz y de cada app, `current_state.md`, auditoría; `DESPLIEGUE.md` marcado obsoleto. Se actualiza con los resultados del hosting. |
| 18 | Rollback | 🟡 Documentado ([08](08-rollback-y-recuperacion.md)); restauración de código probada con el tag. Falta la restauración de base en el hosting. |
| 19 | Commits | ✅ Rama `separacion-altares-catrinas`, sin push ([02 §2](02-plan-separacion-altares-catrinas.md)). |
| 20 | `git status` | ✅ Limpio al cerrar cada fase. |

## 3. Prueba de carga — línea base local (2026-10-17)

**Qué es y qué no es:** apps publicadas en Release (mismo paquete que se sube), Kestrel por HTTPS,
SQL Server LocalDB y el cliente de carga en la **misma máquina** (Xeon E5-2630 v4, 20 hilos). No
mide los 512 MB ni la CPU del hosting; sirve para encontrar errores bajo concurrencia y tener una
referencia. Herramienta: `tools/PruebaCarga` (ver su README). Datos: solo ficticios.

### 3.1 Escenario mixto: 200 usuarios simultáneos, 4 min (178 participantes, 20 jueces, 2 admins)

| App | Peticiones | 5xx + timeouts | 429 | p95 páginas | p95 guardar Preliminar / Final | p99 global aprox. | Working set máx. |
|---|---:|---:|---:|---|---|---|---:|
| Altares | 11,956 | 0 | 0 | ≤ 92 ms | 18 / 14 ms | ≤ 400 ms | 269 MB |
| Catrinas | 13,577 | 0 | 0 | ≤ 54 ms | 16 / 23 ms | ≤ 161 ms | 255 MB |

Incluye login (≈120 ms, dominado por PBKDF2), dashboard, ficha (ver y guardar), búsqueda/listado,
Preliminar, Final (todos los equipos cerrados, lugares verificados en la base), ranking/reporte y
listados de admin.

### 3.2 Escenario PDF (separado): 20 descargas concurrentes, 90 s

| App | Peticiones | Errores | p95 PDF grupal / juez | p95 ZIP individuales | p95 reporte de cierre | Working set máx. |
|---|---:|---:|---|---|---|---:|
| Altares | 2,689 | 0 | 196 / 161 ms | 360 ms | 377 ms | 341 MB (GC servidor) |
| Altares, GC estación + heap limitado a 384 MB | 2,669 | 0 | 125 / 133 ms | 254 ms | 419 ms | **315 MB** |
| Catrinas | 2,247 | 0 | 109 / 109 ms | 219 ms | — | 291 MB |

Desde F4 ambas apps se publican con GC de estación de trabajo (`ServerGarbageCollection=false`).

### 3.3 Rate limiting (sin tocar la configuración)

| Caso | Resultado (ambas apps) |
|---|---|
| 200 logins de cuentas distintas en 60 s desde una sola IP (NAT de la FIM) | 200 exitosos, **0** respuestas 429 |
| Atacante sobre una cuenta: 8 intentos fallidos | intentos 1–5 normales; **6.º en adelante 429**; con la contraseña correcta sigue bloqueada hasta que vence la ventana |
| Otra cuenta desde la misma IP justo después | entra normal (el bloqueo es por cuenta, no por IP) |
| 400 intentos contra cuentas distintas lo más rápido posible | **290 atendidos + 110 con 429**: el techo de 300/min por IP corta (las 10 peticiones previas del mismo minuto completan 300) |

**Decisión:** se mantienen 300/min (login) y 600/min (OAuth) por IP: cubren 200 inicios de sesión
reales en un minuto con 50 % de margen y siguen frenando el abuso masivo. Si en el hosting se
observan 429 legítimos (por ejemplo, por R6 todo el tráfico llegara con la IP de un proxy), se
sube el techo con `Seguridad__RateLimiting__IpLoginPorMinuto` sin recompilar. **No** se toca el
límite por cuenta.

### 3.4 Defectos encontrados y corregidos gracias a la carga

1. **Catrinas, `rowVersion` mal formado → 500** (formulario alterado). Ahora se trata como
   conflicto. Prueba: `RowVersionMalFormado_NoEs500NiGuarda`.
2. **Catrinas, cierres Final simultáneos en la misma carrera → 500** (conflicto de `rowversion`
   al recalcular lugares) con riesgo de lugares desactualizados. Ahora el recálculo se serializa y
   escribe con `ExecuteUpdate`. Prueba: `CierresFinalSimultaneos_MismaCarrera_SinErroresYLugaresCorrectos`
   (fallaba 3 de 8 antes del arreglo).
3. **Altares, condición de carrera al recalcular lugares** (sin `rowversion`, el último en guardar
   ganaba con una foto vieja). Mismo bloqueo. La prueba `LugaresConcurrentesTests` es una
   salvaguarda: no logró reproducir la carrera sin el arreglo.
4. **Ambas, sitio entero en 500 si falta `GoogleAuth:ClientId/ClientSecret`.** Ahora no arranca y
   dice qué falta.
5. **Ambas, enumeración de cuentas por tiempo de respuesta** (cuenta inexistente ~5 ms contra
   ~120 ms). Ahora se hace el mismo trabajo de PBKDF2. Prueba: `SinHashAlmacenado_RechazaConCostoComparable`.
6. **Empaquetado:** el `appsettings.json` local se copiaba al paquete. Hoy solo tiene marcadores y
   LocalDB (se verificó: ningún valor real), pero es el archivo donde se pondrían credenciales. Ahora ningún `appsettings*.json` se publica y `tools/Publicar/publicar.ps1` lo verifica.

### 3.5 Pendiente en el hosting (criterio de aceptación de ADR §11.4)

Correr en `altares.<dominio>` y `catrinas.<dominio>`, con datos ficticios y antes de abrir el
registro real, `preparar` → `login` → `mixto` (10 min) → `pdf` → `abuso`, y anotar aquí:
p95/p99, errores, 429, **memoria máxima de `/…/Diagnostico`** y si hubo reinicios del proceso
(la herramienta los detecta). Umbral: memoria sostenida < 410 MB, 5xx + timeouts < 0.5 %.
**Si no se cumple:** detenerse y comparar complemento de RAM, otro plan de MonsterASP o Azure B1
([06](06-costos-infraestructura.md)). Al terminar: scripts de limpieza y verificación en cero (03 §5).

## 3-bis. Secretos

**Inventario por plataforma** (cada uno con su propio valor; nunca compartido entre apps):

| Secreto | Altares | Catrinas | Dónde vive |
|---|---|---|---|
| Cadena de conexión (usuario y contraseña SQL) | `ConnectionStrings__AltarContext` | `ConnectionStrings__CatrinasContext` | variable de entorno del sitio |
| Google ClientSecret | `GoogleAuth__ClientSecret` | `GoogleAuth__ClientSecret` | variable de entorno del sitio |
| Google ClientId (no secreto, pero no se publica) | `GoogleAuth__ClientId` | `GoogleAuth__ClientId` | variable de entorno del sitio |
| Contraseña del buzón SMTP | `Smtp__Password` (`altares@`) | `Smtp__Password` (`catrinas@`) | variable de entorno del sitio |
| Contraseña temporal del admin semilla | `SEED_ADMIN_PASSWORD` | `SEED_ADMIN_PASSWORD` | variable de entorno, **solo hasta el primer login** |
| Llaves de Data Protection | `App_Data/keys` del sitio | `App_Data/keys` del sitio | archivos generados por la app; no se copian ni versionan |
| Credenciales de la prueba de carga | `PRUEBA_ADMIN_*`, `PRUEBA_CARGA_PASSWORD` | igual | variables de entorno de la máquina que corre la prueba |
| Cuentas de proveedores (MonsterASP, registrador, Google Cloud, webmail `postmaster`) | — | — | gestor de contraseñas del propietario |

No hay más claves: la app no usa API keys de terceros ni secretos de firma propios (Data Protection
genera los suyos).

**Ninguno está versionado (verificado 2026-10-17):** el único `appsettings` en git es la plantilla
`appsettings.Ejemplo.json` de cada app (solo marcadores); `appsettings.json`,
`appsettings.Production.json`, `appsettings.Staging.json`, `keys/` y `App_Data/` están en
`.gitignore`; una búsqueda de patrones de secretos (`GOCSPX-`, `Password=…`, ClientId reales) en
**todo el historial** solo encontró marcadores de documentación y valores de prueba. Los
`appsettings.json` locales tampoco contienen secretos (marcadores y LocalDB con autenticación
integrada). Los paquetes de despliegue no llevan ningún `appsettings*.json`.

## 4. R7 — si Google no devuelve `hd=uabc.edu.mx`

No se relaja nada sin decisión explícita del propietario. Opciones, de más a menos segura:
1. Cliente OAuth **Interno** de la organización UABC (Google solo deja entrar cuentas de la
   organización) y mantener `email_verified` + dominio `@uabc.edu.mx`.
2. Mantener `hd` obligatorio y usar el login local para quien no lo traiga.
3. Solo con autorización escrita: `GoogleAuth__ExigirDominioWorkspace=false` (se siguen exigiendo
   `email_verified=true` y el sufijo `@uabc.edu.mx`), documentado como riesgo aceptado.

## 5. Prueba de independencia en el hosting

| Prueba | Cómo | Esperado |
|---|---|---|
| Apagar Altares no rompe Catrinas | *Stop* del sitio Altares en el panel; usar Catrinas (login, evaluar, constancia) | Catrinas funciona igual |
| Apagar Catrinas no rompe Altares | Lo inverso | Altares funciona igual |
| Cada app usa solo su base | Las variables de cada sitio apuntan a su base; `verificar-limpieza-*.sql` en cada base solo ve sus tablas | Sin cruces |
| Cookies no compartidas | Con sesión en Altares, abrir Catrinas | Catrinas pide login; cookies distintas por host y nombre |
| Data Protection no compartido | `App_Data/keys` distinto por sitio; `ApplicationName` distinto | Una cookie de una app no sirve en la otra |
| Despliegue independiente | Redesplegar solo Catrinas (04 §B.4) | Altares no se reinicia (su `proceso.inicioUtc` en Diagnóstico no cambia) |
| Referencia `ALT-*` blanda | Inscripción de Catrinas con `ALT-…` de un equipo real y otra con un código que no existe | Ambas se guardan; ninguna consulta a Altares |

## 6. Antes de abrir el registro real

- [ ] Gate de privacidad (ADR §8.4) cerrado por el propietario/UABC.
- [ ] R6 y R7 cerrados.
- [ ] Carga en el hosting dentro del criterio.
- [ ] Datos ficticios borrados; `verificar-limpieza-*.sql` en cero en ambas bases.
- [ ] Respaldo manual de ambas bases ya limpias (punto de restauración "vacío").
- [ ] Configuración del periodo revisada en ambas apps (fechas, pesos, umbral).
