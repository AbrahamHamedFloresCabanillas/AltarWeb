# ADR-001 — Separación de AltarWeb en dos plataformas: Altares y Catrinas

| Campo | Valor |
|---|---|
| Estado | **Aprobado conceptualmente** (2026-10-17). Autorizadas solo F0A y F0B. **F3 (Catrinas.Web) bloqueada** hasta definir el modelo de Catrinas (§7.1) |
| Fecha | 2026-10-17 |
| Commit base auditado | `6c800ae` (tag de rollback `pre-separacion`) |
| Rama de trabajo | `separacion-altares-catrinas` |
| Decisión de stack | **A1** — ASP.NET Core + EF Core + SQL Server, monorepo con librería compartida |
| Fecha objetivo | Ambas plataformas en producción **antes del 1 de noviembre de 2026** |

Convenciones: **[CÓDIGO]** verificado en el código · **[DOC]** según documentación del repo ·
**[EXT]** fuente externa con fecha · **[INF]** inferencia · **[PEND]** decisión abierta.

---

## 1. Contexto

1. **[CÓDIGO]** AltarWeb es un monolito ASP.NET Core MVC 8: 5 controladores, ~7.5k líneas de C#,
   ~4.5k de Razor y 23 migraciones. Hasta F0A no tenía tests; F0A agregó `tests/AltarWeb.Tests`.
2. **[CÓDIGO]** El Concurso de Catrinas **no existe como producto**: es un apéndice de la evaluación
   del altar.
   - `EvaluacionCatrina` es 1:1 con `Evaluacion`, con borrado en cascada.
   - El flag `Equipo.HaceCatrina` y 5 pesos en `ConfiguracionPeriodo`.
   - Una pestaña dentro de `NuevaEvaluacion`.
   - Se guarda en el mismo POST, con el mismo juez y el mismo estado que el altar.
   - No tiene constancias propias ni registra a la persona que la personifica.
3. **[EXT, 2026-10-17]** .NET 8 llega a fin de soporte el **2026-11-10**; Microsoft recomienda
   .NET 10 (LTS hasta 2028-11). Fuente: <https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/>.
4. **[Propietario]** La base de producción **no** tiene datos reales y **no** hay histórico de
   Catrina que conservar.
5. **[Propietario, 2026-10-17]** Requisitos:
   - plataformas separadas antes de noviembre;
   - bases de datos separadas;
   - periodos separados;
   - misma identidad visual y monorepo;
   - identidad compartida "si se puede";
   - Catrina con jurado propio, `Preliminar`/`Final` propio y ranking por carrera;
   - inscripciones de Catrina ligadas a un equipo de altar **o** independientes;
   - constancias y captura demográfica iguales a las del programa actual *(el detalle queda
     sujeto a §7.1)*;
   - ~200 usuarios simultáneos;
   - vistas separadas, con posibilidad de "ver TODO".

## 2. Decisión

### 2.1 Estructura

```
AltarWeb/                         (mismo repositorio git)
├─ AltarWeb.sln
├─ src/
│  ├─ Concurso.Core/              librería transversal (compile-time) + Razor Class Library (UI)
│  ├─ Altares.Web/                la app actual, sin Catrina
│  └─ Catrinas.Web/               app nueva (F3, bloqueada hasta §7.1)
└─ tests/
   ├─ Concurso.Core.Tests/
   ├─ Altares.Tests/              (hoy: tests/AltarWeb.Tests, se renombra en F1)
   └─ Catrinas.Tests/
```

### 2.2 Regla arquitectónica de `Concurso.Core`

> **`Concurso.Core` NO puede contener reglas de negocio específicas de Altares ni de Catrinas.**

**Sí puede contener:**
- infraestructura transversal: registro de servicios, middleware, configuración común;
- seguridad: hashing de contraseñas, cookies, cabeceras/CSP, HSTS, ForwardedHeaders, rate limiting,
  antiforgery global, filtro de cambio obligatorio de contraseña;
- la **base** del flujo Google OAuth: identidad verificada server-side, token de un solo uso,
  restricción de dominio `@uabc.edu.mx`;
- privacidad: umbral de agrupación demográfica con piso no anulable;
- fechas y periodos: calendario de Mexicali, periodos `YYYY-1`/`YYYY-2`, fechas límite inclusivas;
- envío de correo;
- **componentes** PDF reutilizables: encabezado institucional, logos, firmas, marca CONFIDENCIAL,
  fuentes;
- el design system: layout, CSS, JS y partials genéricos.

**No puede contener** (cada regla vive dentro de su aplicación):
- equipos, integrantes, roles de equipo;
- fichas y requisitos ("ficha completa", "listo para Final");
- evaluaciones, criterios, fórmulas y pesos;
- rankings y lugares;
- textos y tipos de constancia;
- catálogos propios de un concurso (p. ej. elementos del altar);
- el contenido de los reportes de cada concurso.

**Cómo se hace cumplir:**
- `Concurso.Core` no referencia `Altares.*` ni `Catrinas.*`.
- No define entidades de dominio ni un `DbContext` de negocio.
- Toda adición a Core se revisa contra esta regla en el diff de cada fase.
- Si una regla aparece igual en los dos concursos, **se duplica** en cada app en lugar de subirla a
  Core, salvo que sea un control de seguridad o privacidad.

### 2.3 Reglas de independencia

| Aspecto | Altares | Catrinas | Compartido |
|---|---|---|---|
| Ejecutable / despliegue | propio | propio | — |
| Base de datos SQL Server | propia (conserva su historial de migraciones) | propia (migración base nueva) | **nada** |
| Usuarios, registrantes, jueces, admins | tablas propias | tablas propias | — |
| Configuración por periodo | propia | propia | — |
| Secrets, variables de entorno, cookies, llaves de Data Protection | propios (nombres distintos) | propios | — |
| Cliente OAuth de Google | cliente A | cliente B | mismo proyecto de Google Cloud |
| Buzón de correo | `altares@<dominio>` | `catrinas@<dominio>` | dominio de correo |
| Archivos (uploads) | propios | propios | — |
| Código transversal y UI | — | — | `Concurso.Core` (§2.2, excepción deliberada §2.5) |

Las plataformas **no se llaman entre sí en runtime** ni leen la base de la otra.

### 2.4 Identidad "compartida" sin base compartida

- **Google es el proveedor de identidad común.** Con la misma cuenta `@uabc.edu.mx` se entra a las
  dos plataformas; cada una guarda su propio registro de la persona.
- La **clave natural** entre plataformas es el correo institucional y la matrícula. La usará la
  vista consolidada futura (§2.7) sin acoplar las bases.
- Las cuentas locales con contraseña, si se conservan, son **por plataforma**.

### 2.5 Excepciones al requisito de independencia (documentadas)

1. **`Concurso.Core`**: un cambio ahí obliga a recompilar y redesplegar las dos apps.
   - Es aceptable porque, por §2.2, solo contiene controles transversales que **deben** ser
     idénticos.
   - CI con filtros por ruta.
2. **Proyecto de Google Cloud**: un solo proyecto con dos clientes OAuth (credenciales y URIs de
   callback independientes).
3. **Identidad visual**: mismos assets vía la RCL de Core.
4. **Cuenta de hosting y dominio** (si se elige MonsterASP, §8): las dos apps comparten la cuenta,
   el dominio y el dominio de correo, pero no procesos, bases ni buzones. Un problema de facturación,
   DNS o cuenta afectaría a ambas.

### 2.6 Referencia Altares → Catrinas: identificador público estable (referencia blanda)

No hay FK, ni base compartida, ni comunicación runtime. Catrinas **no** guarda solo un nombre: guarda
un identificador público estable más un snapshot.

**En Altares (cambio pequeño, fase F2):**
- Cada equipo recibe un `CodigoPublico` al crearse, con formato **`ALT-{Periodo}-{NNNN}`**
  (ej. `ALT-2026-2-0042`). `NNNN` es un consecutivo por periodo.
- **Inmutable**: no cambia aunque el equipo se renombre, cambie de carrera o se desactive.
- **Único**: índice único en BD; la asignación concurrente se protege con ese índice y reintento.
- **No revela información sensible**: no contiene nombres, matrículas, carrera ni el `Id` interno.
- **Visible** para el organizador en su panel y en la Ficha de Registro, y para el admin en el
  listado de equipos.

**En Catrinas (F3, sujeto a §7.1):**
- Dos campos opcionales: `AltarExternalId` (p. ej. `ALT-2026-2-0042`) y `AltarNombreSnapshot`
  (nombre del equipo de altar).
- Se capturan **manualmente** al inscribirse; el sistema valida el formato con
  `^ALT-\d{4}-[12]-\d{4}$`.
- **No hay verificación contra Altares**: no hay acceso a su base ni a una API. Es un dato declarado.
- Un administrador de Catrinas puede corregirlo.
- Si Altares desaparece, Catrinas sigue funcionando completamente: los valores son un snapshot que
  nada en Catrinas necesita resolver.
- La vista consolidada futura (§2.7) es la única que podría cruzar `AltarExternalId` con Altares, y
  lo haría fuera de ambas apps.

### 2.7 "Ver todo"

- Cada plataforma tiene su vista y su reporte.
- La **vista consolidada** se difiere a después del concurso. Será un componente aparte, de solo
  lectura, con credenciales de solo lectura a ambas bases.
- Si ese componente cae, ninguna plataforma se ve afectada.
- Al consolidar demografía hay que deduplicar personas por correo/matrícula y aplicar el umbral
  sobre la unión.

### 2.8 Otras decisiones técnicas

- **Versión de .NET**: la decide F0B (§9).
  - Si F0B queda completamente verde, se continúa en **.NET 10**. **[Propietario]** MonsterASP
    soporta .NET 10.
  - Si F0B genera una incompatibilidad relevante, se revierte **solo** el commit de F0B y la
    separación continúa sobre .NET 8.
- **Fuente de PDF: Arial → Lato.** **[CÓDIGO]** QuestPDF ya trae Lato embebida. Quita la
  dependencia de hosting Windows.
- **Data Protection** con almacén de llaves persistente y `ApplicationName` distinto por app (hoy no
  está configurado).
- **CI (GitHub Actions)**:
  - por proyecto: build + tests + `dotnet list package --vulnerable`;
  - los tests de integración requieren LocalDB, así que en CI corren en Windows o se excluyen con
    `--filter Category!=Integracion`;
  - despliegue manual para noviembre.

## 3. Alternativas descartadas

| Opción | Motivo del descarte |
|---|---|
| **B — Reescritura en Next.js + Supabase + Vercel** | Es reescritura, no migración: ~200–360 h contra ~65–160 h de A1. Obliga a rehacer y volver a probar las 22 remediaciones de la auditoría. Incompatible con el plazo y sin una ganancia de mantenimiento proporcional. Vercel Hobby es solo para uso "personal, non-commercial". |
| **C — Híbrida (Altares .NET + Catrinas Next.js)** | Duplica el stack y la superficie de seguridad para una app pequeña que captura datos sensibles. |
| **A2 — Fork completo sin librería compartida** | Las correcciones de seguridad divergen con el tiempo. |
| **Una sola app con "modo" por configuración** | No cumple la operación independiente. |

## 4. Alcance para noviembre (MVP) y lo diferido

**Altares (antes de noviembre):** la funcionalidad actual completa, **sin** Catrina, más el
`CodigoPublico` de §2.6.
- Las tablas y columnas de Catrina se dejan sin uso; se eliminan en una migración posterior, ya
  verificado.

**Catrinas (antes de noviembre):** **alcance pendiente de §7.1.** La capacidad esperada (registro,
inscripciones, backoffice, evaluación `Preliminar`/`Final`, ranking por carrera, constancias) se
concretará en entidades y pantallas **solo después** de resolver esas preguntas. No se copia el
modelo `Equipo` de Altares.

**Diferido a después del concurso:**
- Reporte de Cierre de Catrinas.
- Vista consolidada "TODO".
- Retiro de los modelos legados.
- SEC-08 (`[Authorize]`/claims).
- Recorrido PDF fuera de `wwwroot`.
- CSP sin `unsafe-inline`.
- Regla de empates (pendiente del comité).

## 5. Criterios de aceptación de la separación

1. Altares opera con Catrinas apagada (proceso detenido y su BD inaccesible), y viceversa.
2. Desplegar una app no requiere desplegar la otra. La excepción es un cambio en `Concurso.Core`.
3. Configuración, secrets, variables de entorno, cookies, archivos, evaluaciones y rankings viven en
   recursos distintos. Una prueba por app verifica que su `DbContext` no contiene entidades de la
   otra.
4. Respaldo y restauración independientes de cada base.
5. Todo dato pertenece inequívocamente a una plataforma.
6. La referencia `AltarExternalId` es opcional: ninguna pantalla ni regla de Catrinas falla si
   viene vacía o con un código que ya no existe en Altares.

## 6. Checklist de equivalencia de seguridad

Debe sobrevivir en **ambas** apps. La columna "Prueba" indica qué test lo cubre desde F0A; el resto se
agrega al extraer Core (F1) y en Catrinas (F3).

| Control (hallazgo) | Dónde vive | Prueba |
|---|---|---|
| PBKDF2 + rehash-on-login de SHA-256 legado (SEC-02) | Core | `PasswordHashServiceTests` |
| Identidad de Google server-side y token de un solo uso (SEC-01) | Core | Pendiente: prueba de `CompletarGoogle` sin identidad verificada (F1) |
| Restricción `@uabc.edu.mx` | Core | `RegistranteValidacionTests` |
| Antiforgery global (SEC-10) | Core | `Post_SinTokenAntiforgery_Responde400` |
| Cookie `Secure`/`HttpOnly`/`SameSite`, nombre por app (SEC-05) | Core + config de cada app | `AdminSemilla_LoginConCambioObligatorio…` |
| HTTPS, HSTS, ForwardedHeaders (CFG-02) | Core | `Landing_Responde200ConCabecerasDeSeguridad`, `GoogleLogin_RedirigeAGoogleConCallbackHttps` |
| Cabeceras / CSP (SEC-05) | Core | `Landing_Responde200…` |
| Rate limiting de login (SEC-04) | Core | Pendiente; ver riesgo R1 (§10) |
| Logging de auth sin secretos (LOG-01) | Cada app | Manual |
| Admin semilla por variable de entorno + cambio forzado (SEC-03) | Core; variables por app | `AdminSemilla_LoginConCambioObligatorio…` |
| `ASPNETCORE_ENVIRONMENT=Production` (CFG-01) | Checklist de despliegue | — |
| Índices únicos de concurrencia (SEC-11/15) | Cada `DbContext` | Pendiente (prueba con LocalDB) |
| Final exige requisitos (SEC-13) | Cada app con su regla | `EquipoValidacionHelperTests` |
| Rutas protegidas sin sesión (SEC-08) | Cada app (controlador base en Core) | `RutaProtegida_SinSesion_Redirige` (17 rutas) |
| Umbral demográfico con piso (PRIV-01) | Core | `PrivacidadReporteHelperTests` |
| PDF CONFIDENCIAL (PRIV-02) | Core (componente PDF) | Pendiente |
| ViewModels sin binding de entidades (SEC-12) | Cada app | Revisión de diff |
| Scaffolding solo en Debug (DEP-01) | Cada `.csproj` | Revisión de diff |
| Modelo EF sin cambios pendientes | Cada app | `ElModeloNoTieneCambiosSinMigracion` |
| Data Protection persistente (nuevo) | Core + config | Pendiente (F1) |

**Defecto encontrado en F0A, no corregido:**
`PasswordHashService.EsFormatoReconocido` trata como hash un texto plano que es Base64 válido
(p. ej. `"1234"`). Por eso el fixup de arranque de `Program.cs` no lo re-hashea.
- Solo afecta a Jueces legados con contraseña en texto plano.
- No abre un acceso: la verificación falla y el login se rechaza. Pero deja la contraseña plana en
  la BD.
- Queda una prueba en `Skip` que lo documenta. **[PEND]** Corregirlo en F1 al mover el servicio a
  Core.

## 7. Decisiones abiertas

### 7.1 Bloqueo de F3 — modelo de Catrinas (no bloquea F0, F1 ni F2)

Antes de diseñar entidades o migraciones de `Catrinas.Web` hay que definir con el propietario:

1. Qué representa una **inscripción** de Catrina.
2. Si concursa **una persona o un equipo**.
3. **Quién personifica** a la Catrina y cómo se registra.
4. Si existen **integrantes o colaboradores**, y con qué roles.
5. **Quién es responsable** de la inscripción.
6. **Quién aparece en el ranking**.
7. **Quién recibe constancia**.
8. Qué **tipos de constancia** corresponden realmente.
9. Si el **maestro encargado** es obligatorio, opcional o no existe.
10. Qué **datos personales y demográficos** captura cada participante.

Mientras no se resuelvan, no se copia el modelo `Equipo` de Altares ni se escribe código de
dominio de Catrinas.

### 7.2 Otras decisiones (no bloquean el arranque)

| Tema | Supuesto por defecto |
|---|---|
| ¿Jueces y admins son los mismos? | Cuentas separadas por plataforma; la misma persona se registra en ambas |
| Registro local con contraseña (PRIV-05) | Se conserva en Altares. En Catrinas se decide junto con §7.1 |
| Dominio | Necesario antes de F4 si se usa MonsterASP (§8); un `.com` con `altares.` y `catrinas.` |
| Presupuesto | ~$35 USD el primer año con la recomendación de §8; pendiente de confirmar |
| Sitio `altaruabc.runasp.net` / BD `db66464` (DESPLIEGUE.md) | **[PEND]** Confirmar si existe; si está abandonado, rotar o eliminar las credenciales de `AltarWeb-publish.zip` |
| Rate limiting con NAT institucional | Ver riesgo R1 (§10) |

## 8. Hosting, costos y correo

Datos **[EXT]** consultados el **2026-10-17** en las páginas oficiales.
- **Tipo de cambio:** DOF del 16-10-2026, 1 USD = 18.425 MXN.
- **Sin impuestos.** Si aplica IVA, ×1.16; no se verificó si MonsterASP lo cobra.

### 8.1 Opciones evaluadas (dos plataformas)

| Opción | Desembolso inicial real | Costo del primer año | Notas |
|---|---|---|---|
| **MonsterASP Premium Single** ← recomendada | **$23.40 USD (431 MXN)**, factura **anual** por adelantado | $23.40, más dominio. Renovación $30/año | Equivale a $1.95/mes, pero **no se paga mensual**: es un solo cargo al año. Incluye 1 sitio + 3 "subdomain websites" independientes (wwwroot, .NET y HTTPS propios, procesos separados), **2 bases MSSQL de 2 GB**, 512 MB de RAM por sitio, backups diarios, datacenter en EE. UU. (Salt Lake City) o UE, Let's Encrypt automático y correo con 10 GB de almacenamiento. **Requiere dominio propio.** CPU no publicada. |
| Azure: App Service Linux B1 + 2× Azure SQL Basic | Por uso: ~$22.94 el mes del concurso | Mínimo ~$13; 24/7 ~$275 | Funciona sin dominio. El SQL gratuito queda al límite en un concurso. |
| SmarterASP.NET Advance | Pago trimestral o anual | ~$59 | Alternativa equivalente a MonsterASP. |
| Vercel Pro + Supabase Pro (referencia, opción B) | $55 al mes | $660 | Sin contar la reescritura. |

Planes gratuitos (MonsterASP Free, Azure F1, Supabase Free) **descartados para producción**.

### 8.2 Recomendación de hosting

**MonsterASP Premium Single + un dominio `.com`.**
- **Costo total aproximado del primer año:** **~$34.5 USD (~636 MXN)** sin IVA: plan $23.40 +
  dominio ~$11.08 (Porkbun, tarifas incluidas).
- **Sujeto a:**
  - la prueba de carga de §11;
  - el gate de datos sensibles de §8.4.

**Plan B:** Azure App Service Linux B1 + Azure SQL Basic.

### 8.3 Correo (recomendación cambiada respecto a la versión anterior)

**Primera opción: los buzones del propio MonsterASP Premium**, con SMTP autenticado:
`altares@<dominio>` y `catrinas@<dominio>`.

**Verificado [EXT, 2026-10-17]** en la ayuda de MonsterASP ("Sending e-mails from your applications"):
- **Límite: 300 correos por hora por buzón.** Cada plataforma tiene su propia cuota.
- **SMTP autenticado:** `mailXXX.mailasp.net`; usuario = dirección completa del buzón.
  - Puerto **587 con STARTTLS**, o 465 con SSL implícito.
  - **Usaremos 587**: `System.Net.Mail.SmtpClient`, que usa `ConstanciaService`, no soporta SSL
    implícito en 465.
- **DKIM:** se habilita desde el webmail entrando como `postmaster` (Domain Settings). Con buzón,
  los mensajes se firman en cuanto DKIM está activo, aunque el DNS esté fuera de MonsterASP.
- **SPF:** registro TXT en la raíz del dominio.
- **DMARC:** no lo documentan; se agrega un registro `_dmarc` (inicialmente `p=none`) como parte
  de F4.
- El remitente debe ser un dominio propio: no se permiten direcciones de dominios de MonsterASP.

**No verificado:** la página de precios no indica cuántos buzones ni dominios de correo incluye
Premium Single; solo dice "10 GB e-mail storage", y el "dominio de correo adicional" cuesta +$1/mes.
Los **10 buzones** que indicó el propietario se confirman en el panel al contratar. Se necesitan
solo 2.

**Contraindicaciones revisadas:** ninguna bloqueante. Dos consideraciones:
1. El envío masivo debe respetar 300 por hora. El envío actual es manual, por evaluación (≤ ~10
   correos por equipo), así que no se acerca al límite. Si en el futuro se envía todo de golpe,
   hay que encolar o espaciar los envíos.
2. Ambas plataformas comparten la reputación del dominio de correo (excepción de §2.5).

**Fallback:** Gmail con contraseña de aplicación (~500 por día; Google la marca como "no
recomendada"). Las constancias siempre se pueden descargar aunque falle el correo.

### 8.4 Gate de hosting y datos sensibles (antes de cargar información real)

No se carga ningún dato real (registrantes, demografía) hasta cerrar **todos** estos puntos:

1. **Ubicación definitiva del datacenter.** Premium ofrece UE o EE. UU. (Salt Lake City). Si se
   elige EE. UU. por latencia, **los datos personales, incluidos los sensibles (género y
   autodescripción cultural), quedarán almacenados en Estados Unidos**. El aviso de privacidad debe
   decirlo.
2. **DPA / tratamiento de datos.** La página de precios y la portada de MonsterASP no mencionan un
   DPA ni GDPR; solo enlazan una Privacy Policy. Hay que revisarla y, si no basta, solicitar un DPA
   al proveedor.
3. **Compatibilidad con el aviso de privacidad y las políticas aplicables** al concurso y a la UABC:
   transferencia internacional de datos personales sensibles. Esta confirmación la da el
   propietario o la instancia responsable de la UABC; no es una decisión técnica.
4. Hasta cerrar el gate, el entorno en línea solo se usa con **datos ficticios** (scripts de
   demostración y la prueba de carga).

### 8.5 CI

GitHub Actions Free: 2,000 minutos al mes en repos privados.

## 9. Plan de fases

Cada fase cierra con: build, todos los tests, revisión del diff, `/security-review` si toca auth,
documentación, y **commit independiente**.

| Fase | Contenido | Estado / esfuerzo (h) |
|---|---|---|
| **F0A** | Rama, tag `pre-separacion`, suite mínima de tests, baseline en .NET 8 | **Hecha** — commit `9173f4c` |
| **F0B** | .NET 8 → 10 + EF Core 10; build; todos los tests; verificar QuestPDF, OAuth, migraciones, sesiones y arranque. Commit reversible | Autorizada; 2–6 |
| F1 | Monorepo + extracción de `Concurso.Core` **sin cambio de comportamiento** | 10–20 |
| F2 | Altares sin Catrina + `CodigoPublico` (§2.6) | 6–10 |
| **F3** | Catrinas.Web | **Bloqueada por §7.1**; 30–55, a re-estimar |
| F4 | Despliegue, OAuth ×2, buzones ×2 con SPF/DKIM/DMARC, prueba de carga (§11), pruebas de independencia (§5), gate de §8.4 | 10–18 |
| Docs | `docs/00–09`, actualización de README, current_state y DESPLIEGUE | 6–10 |

El propietario necesita **~6–10 h propias**: cuentas, dominio, Google Cloud, buzones, revisión del
gate de §8.4 y pruebas de aceptación.

**Fallback de calendario:** si al terminar F2 el plazo no alcanza, se despliega solo Altares
separado y Catrinas opera este año con `pre-separacion`. Requiere aceptación del propietario.

## 10. Riesgos

| Id | Riesgo | Mitigación |
|---|---|---|
| **R1** | **El rate limiting de login (10 por minuto por IP) bloqueará a usuarios legítimos el día del concurso.** Jueces y participantes en la red de la FIM salen por la misma IP pública (NAT), así que el 11.º login de ese minuto, de *cualquier* persona, es rechazado. La prueba de carga, que sale de una sola IP, también lo dispara. | **[PEND]** Antes de F4: volver el límite configurable y particionar por IP + cuenta (p. ej. 10/min por cuenta y 60–120/min por IP). Decisión de seguridad del propietario, porque cambia SEC-04. |
| R2 | 512 MB de RAM y CPU no publicada para ~200 usuarios y generación de PDFs | Prueba de carga (§11); complemento de RAM o plan B |
| R3 | Plazo de ~14 días con F3 aún sin definir | Resolver §7.1 cuanto antes; fallback de §9 |
| R4 | DPA o ubicación de datos incompatibles con políticas de la UABC | Gate de §8.4; Azure con región elegible como alternativa |
| R5 | Correo marcado como spam | SPF + DKIM + DMARC en F4; prueba de envío a cuentas `@uabc.edu.mx` |

## 11. Prueba de carga (antes de F4, contra el hosting real con datos ficticios)

**Objetivo:** decidir con datos si 512 MB por aplicación bastan.

**Entorno:**
- Se corre contra el sitio desplegado en MonsterASP, porque una prueba local no dice nada de esos
  512 MB.
- Base con datos sintéticos: 13 carreras, ~130 equipos, ~600 registrantes, ~30 jueces.
- R1 resuelto o con límite elevado solo para la ventana de la prueba.
- Herramienta propuesta: **k6** (open source). Se pedirá autorización antes de instalarla.

### 11.1 Escenario mixto (~200 usuarios virtuales)

- Rampa de 0 a 200 en 2 min, sostener 10 min, bajar en 1 min.
- Tiempo de espera entre acciones: 3–10 s.

| Perfil | Usuarios | Flujo |
|---|---|---|
| Participantes | ~150 | Login → Dashboard → Ficha → Dashboard |
| Jueces | ~40 | Login → Historial → avance por carrera → NuevaEvaluacion (búsqueda por carrera) → GuardarEvaluacion `Preliminar` → reedición → cierre `Final` (recalcula lugares) |
| Admins | ~10 | Login → RegistrantesYEquipos (ambas pestañas) → Jueces → Configuración (lectura) |

Accesos simultáneos a BD: los jueces escriben evaluaciones distintas a la vez y, de forma
deliberada, **dos jueces sobre el mismo equipo** para verificar SEC-15 bajo carga.

### 11.2 Prueba de PDF (separada)

- Ráfagas concurrentes de 5, 10 y 20 descargas de constancias individuales en ZIP (equipo de ~8
  integrantes), más 50 descargas grupales secuenciales.
- Se observa el pico de memoria y CPU de QuestPDF, y si el proceso se recicla.

### 11.3 Qué se registra

- p50 / p95 / p99 por endpoint.
- Errores HTTP por código.
- Timeouts.
- RAM del proceso (panel de MonsterASP; si no basta, un endpoint de diagnóstico temporal, solo
  admin, con *working set* y heap del GC, que se retira después).
- Reciclajes del app pool (log `stdout` / tiempo de actividad).
- SQL Server: consultas > 500 ms (log de EF), timeouts, deadlocks y agotamiento del pool de
  conexiones.

### 11.4 Criterio de aceptación (por aplicación)

| Métrica | Umbral |
|---|---|
| p95 de páginas (GET) | < 1.5 s |
| p95 de GuardarEvaluacion / Final | < 3 s |
| p99 global | < 5 s |
| Errores 5xx + timeouts | < 0.5 %, sin errores de SQL por pool o deadlock no manejado |
| Memoria sostenida | < 80 % de 512 MB (~410 MB), sin reciclajes del proceso |
| PDF | 10 ZIP concurrentes completan < 15 s c/u sin reciclaje; con 20 se acepta degradación pero no caída |

**Si no se cumple:** primero el complemento de RAM (~$1/mes). Si el cuello de botella es la CPU,
o sigue fallando, se pasa al plan B (Azure B1).

## 12. Datos, migración y rollback

- No hay datos reales ni histórico de Catrina. Altares conserva su esquema y migraciones; Catrinas
  empieza con una base vacía.
- **Rollback de código:**
  - tag `pre-separacion`;
  - cada fase es un commit independiente;
  - F0B se revierte con `git revert` sin afectar F0A.
- **Rollback de datos:**
  - respaldo de cada base antes de cada migración en producción;
  - la eliminación de columnas y tablas de Catrina en Altares va en una migración separada y
    posterior.
