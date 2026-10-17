# Despliegue gratuito de AltarWeb — guía paso a paso

> ⚠️ **Documento parcialmente obsoleto (2026-10-17).** Describe el despliegue de la app única anterior
> a la separación en Altares y Catrinas. En la rama `separacion-altares-catrinas` ya cambiaron:
> - el proyecto, que ahora es `src/Altares.Web/Altares.Web.csproj`;
> - el ejecutable, que ahora es `Altares.Web.exe` (en `web.config`, `processPath=".\Altares.Web.exe"`);
> - el runtime, que ahora es **.NET 10**;
> - la configuración, que suma `Seguridad:RateLimiting` y `Seguridad:ForwardedHeaders`;
> - Google, que ahora exige `email_verified` y `hd`.
>
> Lo sustituirán `docs/04-despliegue-altares.md` y `docs/05-despliegue-catrinas.md` (fase F4).

> Escrito el 2026-09-02 para el commit `07ead92`. Complementa la sección
> *Checklist de despliegue a producción* del [`README.md`](README.md).

## Resumen de la decisión

AltarWeb es **ASP.NET Core 8 + SQL Server**, y esa combinación descarta casi todo el
hosting gratuito moderno (Render, Fly, Railway y similares dan Postgres, no SQL Server, y
migrar el proveedor de EF Core sería trabajo real). Las dos opciones gratuitas viables:

| | **MonsterASP.NET (free)** ← recomendada | **Azure (App Service F1 + Azure SQL free)** |
|---|---|---|
| Costo | Gratis, sin tarjeta | Gratis, pero requiere suscripción de Azure |
| Base de datos | MSSQL 1 GB incluida | Azure SQL: 100,000 vCore-segundos y 32 GB al mes, **de por vida** |
| Cambios de código | **Ninguno** | Ninguno |
| Tiempo hasta estar arriba | ~20 minutos | ~1–2 horas (incluye verificación estudiantil) |
| Límites | 256 MB RAM, 5 GB disco, 1 sitio, subdominio `*.runasp.net`, datacenter en Europa | 60 minutos de CPU **por día** (al agotarse, el sitio devuelve 403), 1 GB RAM |
| HTTPS | Let's Encrypt gratis, **renovación manual cada 90 días** | Incluido y automático en `*.azurewebsites.net` |
| Para qué sirve | Demo, pruebas con el comité, presentación | Lo mismo, con mejor camino a producción de verdad |

**Recomendación:** MonsterASP para tener el sistema en línea hoy. Para el concurso real
(decenas de jueces y equipos usándolo al mismo tiempo, con datos personales reales),
ninguno de los dos planes gratuitos aguanta: usa el crédito de **Azure for Students**
($100/año con correo `@uabc.edu.mx`) con un App Service **B1**, o el plan de paga de
MonsterASP (~3 USD/mes).

> ⚠️ **Sobre los datos:** el sistema guarda género y autodescripción cultural, que son
> datos personales sensibles bajo la ley mexicana (es justo lo que protege la regla de
> umbral de `vision.md` §12.2). Mientras esté en hosting gratuito compartido, **usa datos
> ficticios**. No subas el padrón real de participantes a un plan gratuito.

---

## Lo que ya está preparado

En `C:\Users\Abram\Desktop\Programa Altar\AltarWeb-publish.zip` está la aplicación **ya
compilada en Release** y lista para subir.

> ⚠️ **El ZIP actual se generó el 2026-09-02 desde `07ead92`** y no incluye los cambios de
> `c615f9a` (escala de 3 niveles, validación de ficha, edición administrativa). Antes de
> subirlo hay que regenerarlo con el comando de abajo y volver a aplicar los ajustes de la
> tabla siguiente.

Se genera con:

```bash
dotnet publish src/Altares.Web/Altares.Web.csproj -c Release -r win-x86 --self-contained false -o <carpeta>
```

> **`win-x86`, no `win-x64`.** El app pool de MonsterASP corre en **32 bits**. Con un
> publish de `win-x64` el sitio devuelve `HTTP 500.30` y el log del módulo dice
> *"Found dotnet.exe matching current process architecture (x86)"* seguido de *"CLR worker
> thread exited prematurely"*: el apphost y las DLL nativas (`Microsoft.Data.SqlClient.SNI.dll`,
> `QuestPdfSkia.dll`) son de 64 bits y no cargan en un proceso de 32.

Incluye:

- El `web.config` con `ASPNETCORE_ENVIRONMENT=Production` y las variables del admin semilla
  ya declaradas (solo hay que poner la contraseña temporal).
- Un `appsettings.json` de plantilla, con la cadena de conexión marcada para reemplazar.
- La carpeta `wwwroot/uploads/recorridos/` vacía y lista para recibir el recorrido PDF.
- **Sin** la herramienta de scaffolding vulnerable (verificado: el build de Release no la
  empaqueta — hallazgo DEP-01 de la auditoría).

### Los tres archivos que hay que retocar después de cada `dotnet publish`

`dotnet publish` **regenera** `web.config`, `appsettings.json` y `AltarWeb.runtimeconfig.json`,
así que pisa los ajustes del despliegue. Después de republicar, revisa:

| Archivo | Qué hay que dejarle |
|---|---|
| `appsettings.json` | La cadena de conexión de MonsterASP (el publish la reemplaza por la de LocalDB). |
| `web.config` | `processPath=".\AltarWeb.exe"` y el bloque `<environmentVariables>` con `ASPNETCORE_ENVIRONMENT=Production`. |
| `AltarWeb.runtimeconfig.json` | `"rollForward": "major"` y `"System.GC.Server": false`. |

`System.GC.Server: false` importa: con los 256 MB de RAM del plan gratuito, el GC de
servidor (el default de ASP.NET Core) reserva de más y es una causa común de que el proceso
muera al arrancar. `rollForward: major` deja que la app corra si el host solo tiene un .NET
más nuevo que el 8 instalado para x86.

### Para reemplazar `AltarWeb.dll` hay que apagar la app primero

Con `hostingModel="inprocess"`, IIS carga el DLL dentro de `w3wp.exe` y lo deja **bloqueado**:
subirlo por FTP falla con `550 File unavailable`. El procedimiento estándar:

1. Sube un archivo `app_offline.htm` (cualquier HTML) a la raíz del sitio → el módulo apaga
   la aplicación y suelta los archivos.
2. Espera unos segundos y sube los DLL.
3. Borra `app_offline.htm` → la aplicación vuelve a arrancar.

Los archivos de configuración (`web.config`, `appsettings.json`, `runtimeconfig.json`) sí se
pueden reemplazar en caliente. Subir el `web.config` **recicla el app pool**, que es la forma
de hacer que la app relea `appsettings.json`: las credenciales de Google y la cadena de
conexión se leen **una sola vez al arrancar**, así que cambiarlas sin reciclar no surte efecto.

---

## Pasos — MonsterASP.NET

> **Estado del despliegue (2026-09-02): pasos 1 a 5 completados.** El sitio es
> **`altaruabc.runasp.net`**, la base de datos **`db66464`** está creada, HTTPS ya quedó
> activado, y el `AltarWeb-publish.zip` ya trae dentro la cadena de conexión y la
> contraseña temporal del admin semilla. **Continúa en el paso 6.**

### 1. Crear la cuenta
Entra a <https://www.monsterasp.net/ASP.NET-Freehosting/> y regístrate. No pide tarjeta.
Confirma el correo.

### 2. Crear el sitio
En el panel, **Websites → Create website**:
- Nombre/subdominio: el elegido fue `altaruabc` → `altaruabc.runasp.net`.
- Versión: **.NET 8**.
- Hosting model: **InProcess** (es el que trae el `web.config` del paquete).

### 3. Crear la base de datos
**Databases → Create database → MSSQL**. Anota usuario, contraseña y servidor.
Copia la **cadena de conexión** que te muestra el panel; se ve parecida a:

```
Server=db-xxxx.monsterasp.net; Database=db_xxxxxx; User Id=db_xxxxxx_admin; Password=xxxxx; TrustServerCertificate=True; Encrypt=True;
```

### 4. Activar HTTPS — **no te lo saltes**
**HTTPS → Let's Encrypt → Enable HTTPS**.

Es obligatorio: la cookie de sesión está marcada como `Secure` (corrección SEC-05 de la
auditoría), así que **sobre HTTP nadie puede iniciar sesión**. En el plan gratuito el
certificado dura 90 días y **se renueva a mano** — ponte un recordatorio.

### 5. Preparar los dos archivos de configuración
Descomprime `AltarWeb-publish.zip` en una carpeta local y edita:

**`appsettings.json`** → pega la cadena de conexión del paso 3:
```json
"ConnectionStrings": {
  "AltarWebContext": "Server=db-xxxx.monsterasp.net; Database=...; User Id=...; Password=...; TrustServerCertificate=True; Encrypt=True;"
}
```

**`web.config`** → cambia el valor de `SEED_ADMIN_PASSWORD` por una contraseña temporal
tuya (larga, de un solo uso — la vas a cambiar en el primer login).

### 6. Subir los archivos
En el panel, **Websites → File Manager**, o por FTP con FileZilla usando las credenciales
que te da **Websites → FTP accounts**. Sube **todo el contenido** del ZIP a la carpeta
`wwwroot` del sitio (que el `AltarWeb.dll` y el `web.config` queden en la raíz de esa
carpeta, no dentro de una subcarpeta).

> Alternativa más cómoda si usas Visual Studio: el panel ofrece un archivo de
> *Web Deploy* (`.PublishSettings`) → **Publicar → Importar perfil** y publicas con un clic.
> Si haces esto, acuérdate de que Visual Studio va a regenerar el `web.config`: vuelve a
> agregar las `<environmentVariables>` después.

### 7. Primer arranque
Abre `https://altaruabc.runasp.net`.

La primera petición tarda (arranque en frío). Al iniciar, la aplicación **crea el esquema
sola**: `SeedData.Initialize` ejecuta `Database.Migrate()`, aplica todas las migraciones pendientes (23 al commit `c615f9a`),
carga los catálogos (13 carreras, géneros, 24 elementos) y crea el admin semilla con la
contraseña que pusiste en el `web.config`.

### 8. Entrar y asegurar la cuenta
- Pestaña **Jueces / Admin** → usuario `abram` + la contraseña temporal.
- El sistema te va a **obligar** a cambiarla antes de dejarte hacer nada (filtro
  `ForzarCambioPasswordFilter`, corrección SEC-03).
- Después de cambiarla, **borra las dos líneas de `SEED_ADMIN_*` del `web.config`** y
  vuelve a subirlo. Ya no sirven y no tiene caso dejar una contraseña en un archivo.

### 9. Google OAuth (opcional, pero es la mitad del login)
En [Google Cloud Console](https://console.cloud.google.com) → *APIs y servicios →
Credenciales → ID de cliente OAuth 2.0*:
- **Orígenes autorizados de JavaScript:** `https://altaruabc.runasp.net`
- **URI de redirección autorizados:** `https://altaruabc.runasp.net/Registro/google-callback`

Copia `ClientId` y `ClientSecret` a `appsettings.json` y vuelve a subirlo. Sin esto, el
botón "Continuar con Google" falla; el login local sigue funcionando.

### 10. Correo (constancias)
En `appsettings.json`, sección `Smtp`: usa una **contraseña de aplicación** de Gmail (no la
contraseña normal de la cuenta) y el puerto 587. Si el hosting bloquea el SMTP saliente,
las constancias se seguirán **descargando** bien; solo fallaría el envío por correo.

---

## Checklist de verificación después de subir

- [ ] `https://` funciona y el candado sale bien (sin esto no hay login).
- [ ] Login de admin entra y te fuerza el cambio de contraseña.
- [ ] `SEED_ADMIN_PASSWORD` ya fue borrado del `web.config`.
- [ ] `/AltarAdmin/Configuracion` abre y deja subir el recorrido PDF (prueba con un PDF de <25 MB).
- [ ] Registro de un participante de prueba con correo `@uabc.edu.mx`.
- [ ] Crear equipo + Ficha de Registro completa.
- [ ] Evaluación `Preliminar` → `Final` y descarga de constancia (valida que QuestPDF corre en el servidor).
- [ ] `/AltarAdmin/ReportePeriodo` genera y descarga el PDF con la marca de agua CONFIDENCIAL.

Si algo truena, el error queda en `wwwroot/logs/stdout*.log`: cambia
`stdoutLogEnabled="false"` a `"true"` en el `web.config`, reproduce el error, lee el log y
**vuelve a ponerlo en `false`** (el log crece sin límite).

---

## Notas técnicas verificadas para este despliegue

- **Migraciones automáticas:** no hace falta correr `dotnet ef database update` contra el
  servidor. `Program.cs` → `SeedData.Initialize` → `context.Database.Migrate()` lo hace al
  arrancar, contra una base vacía.
- **QuestPDF necesita Windows aquí:** las constancias y el reporte usan la fuente
  **Arial** (`ConstanciaService.cs:174`, `ReportePeriodoService.cs:305`). En un host Linux
  (Render, App Service Linux) Arial no existe y los PDF saldrían con otra fuente o
  fallarían. MonsterASP e IIS son Windows, así que no hay problema. Si algún día se migra a
  Linux, hay que empaquetar la fuente o cambiar a una familia incluida en QuestPDF (Lato).
- **Archivos subidos:** el recorrido PDF se guarda en `wwwroot/uploads/recorridos/`, que en
  IIS es disco real y **persiste** entre reinicios. En un contenedor (Render, App Service
  Linux) se perdería en cada redeploy.
- **`ForwardedHeaders` ya está configurado** (`Program.cs:116-126`), así que el sitio
  detecta bien el `https` detrás del proxy del hosting.
- **La base de datos solo es visible desde adentro de MonsterASP.** `db66464.databaseasp.net`
  resuelve a `10.0.0.31`, una IP privada de su red: desde tu máquina no se puede conectar
  (verificado, TCP 1433 no responde). Eso no afecta a la aplicación —el sitio corre en esa
  misma red— pero significa que no se pueden aplicar migraciones ni consultar la base desde
  la computadora local. Si quieres usar SQL Server Management Studio, primero hay que
  activar **Remote access** en el panel de la base, que expone otra dirección de servidor.
- **Deuda conocida que aplica en línea:** el recorrido PDF queda accesible por URL directa
  sin sesión (`/uploads/recorridos/recorrido-2026-2.pdf`). Está documentado en
  `current_state.md` y en el hallazgo E de `AUDITORIA_SEGURIDAD.md`.

---

## Alternativa: Azure (App Service F1 + Azure SQL free)

Si prefieres el camino institucional, y sobre todo si vas a pasar a producción real:

1. Activa **Azure for Students** en <https://azure.microsoft.com/free/students/> con tu
   correo `@uabc.edu.mx` (no pide tarjeta; da $100 de crédito por 12 meses, renovable).
   Ojo: el offer *Azure for Students **Starter*** no es compatible con el free de SQL; usa
   el de *Azure for College Students*.
2. Crea la base con el free offer desde <https://aka.ms/azuresqlhub> → **Start free**
   (100,000 vCore-segundos + 32 GB al mes, de por vida, con auto-pausa al agotarse).
3. Crea un **App Service** en **Windows**, plan **F1 (gratis)**. Windows importa por lo de
   Arial/QuestPDF.
4. En *Configuración → Cadenas de conexión*, agrega `AltarWebContext` con la cadena de
   Azure SQL; en *Configuración de la aplicación*, agrega `ASPNETCORE_ENVIRONMENT=Production`
   y `SEED_ADMIN_PASSWORD`.
5. En *Redes / SQL Server → Firewall*, activa **"Permitir que los servicios de Azure
   accedan al servidor"**, o el App Service no podrá conectarse.
6. Publica desde Visual Studio (*Publicar → Azure → App Service*) o con GitHub Actions.
7. Google OAuth: la URI de redirección es
   `https://<tu-app>.azurewebsites.net/Registro/google-callback`.

El límite de **60 minutos de CPU al día** del plan F1 es el problema serio: al agotarse, el
sitio responde 403 hasta el día siguiente. Para el día del concurso, sube a **B1** (~13
USD/mes, sale del crédito de estudiante) y bájalo después.

---

## Fuentes consultadas (2026-09-02)

- [MonsterASP.NET — Free hosting](https://www.monsterasp.net/ASP.NET-Freehosting/) y [Precios](https://www.monsterasp.net/Pricing/)
- [MonsterASP.NET — Activar HTTPS con Let's Encrypt](https://help.monsterasp.net/books/https/page/how-to-activate-https-with-lets-encrypt-certificate)
- [MonsterASP.NET — Modelos de hosting de ASP.NET Core](https://help.monsterasp.net/books/websites/page/aspnet-core-hosting-model-support)
- [Azure SQL Database free offer](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer?view=azuresql)
- [Azure for Students](https://azure.microsoft.com/en-us/free/students/)
- [Planes de Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/overview-hosting-plans)
