# 04 — Despliegue de Altares (y la infraestructura común)

> Fecha de esta guía: 2026-10-17. Fase F4 del [ADR-001](01-decision-arquitectura.md).
> Sustituye a [`DESPLIEGUE.md`](../DESPLIEGUE.md), que queda **obsoleto**.
>
> La parte A es común a las dos plataformas y se hace **una sola vez**. La parte B es solo de Altares.
> Catrinas sigue el mismo patrón en [05-despliegue-catrinas.md](05-despliegue-catrinas.md).
>
> Ningún valor secreto aparece en este documento ni en git. Donde dice `<…>`, el valor real lo
> escribe el propietario **únicamente** en el panel del proveedor.

---

## A. Infraestructura común

### A.1 Estructura de dominios (decisión)

| URL | Qué es | Sitio en MonsterASP |
|---|---|---|
| `https://altares.<dominio>` | Concurso de Altares (`Altares.Web`) | *Subdomain website* 1 |
| `https://catrinas.<dominio>` | Concurso de Catrinas (`Catrinas.Web`) | *Subdomain website* 2 |
| `https://<dominio>` y `https://www.<dominio>` | Portada estática con dos enlaces (sin app ni base) | Sitio principal del plan |
| — | Libre (reservado para una app futura, p. ej. un "ver todo") | *Subdomain website* 3 |

**Por qué subdominios de un dominio propio y no otra cosa:**
- Cada app queda en su propio *host*, así que las cookies de sesión (que no fijan `Domain`) **no
  se comparten** entre Altares y Catrinas aunque compartan dominio padre. Además, cada app usa su
  propio nombre de cookie y su propio `ApplicationName` de Data Protection.
- MonsterASP Premium solo admite dominios propios en sitios Premium y da exactamente 1 sitio + 3
  subdominios: alcanza para las dos apps, la portada y una plaza libre.
- **Alternativa considerada:** subdominios institucionales (`altares.fim.uabc.edu.mx`). Dan más
  confianza a los usuarios, pero requieren que TI de la UABC cree registros DNS en su zona, con
  plazos que no controlamos antes de noviembre. Si se consiguen después, basta con agregarlos como
  dominio adicional del mismo sitio y registrar los nuevos callbacks OAuth (sección A.6), sin
  cambiar código.
- **No usar** `*.runasp.net` (el dominio temporal del proveedor) para producción: los callbacks de
  Google y el remitente de correo deben ser del dominio propio.

### A.2 Qué contratar (lo hace el propietario; nada se compra automáticamente)

1. **Dominio `.com`** en el registrador que prefieras (referencia: Porkbun ~11.08 USD el primer
   año, ver [06-costos-infraestructura.md](06-costos-infraestructura.md)).
2. **MonsterASP Premium Single, facturación anual.** Al contratar, elige el **datacenter**: esta
   elección es parte del gate de privacidad (ADR §8.4) y no se puede cambiar después sin migrar.
   Antes de cargar datos reales tiene que estar resuelto ese gate.

### A.3 DNS

**Opción recomendada (A): delegar el dominio a MonsterASP.** En el registrador, cambia los
*nameservers* a `ns1.machineasp.net`, `ns2.machineasp.net` y `ns3.machineasp.net` (hasta 24 h).
El panel crea y mantiene solos los registros del sitio y del correo.

**Opción B: conservar el DNS del registrador** y crear tú los registros. Los valores exactos
(`<IP del panel>`, `siteXXXX.siteasp.net`, `mailXXX.mailasp.net`) aparecen en el asistente
*Add domain* del panel con un botón *Copy*:

| Nombre | Tipo | Valor |
|---|---|---|
| `<dominio>` | A | `<IP del panel>` (un solo registro A; uno sobrante impide emitir el certificado) |
| `www.<dominio>` | CNAME | `<servidor del sitio principal>.siteasp.net` |
| `altares.<dominio>` | A o CNAME | el que indique el panel para el *subdomain website* de Altares |
| `catrinas.<dominio>` | A o CNAME | el que indique el panel para el *subdomain website* de Catrinas |
| `<dominio>` | MX | `mailXXX.mailasp.net`, prioridad 10 |
| `<dominio>` | TXT | `v=spf1 a mx include:spf.mailasp.net ~all` |
| `autodiscover.<dominio>` | CNAME | `mailXXX.mailasp.net` |
| `_autodiscover._tcp.<dominio>` | SRV | `10 10 443 mailXXX.mailasp.net` |
| `_dmarc.<dominio>` | TXT | `v=DMARC1; p=none; rua=mailto:postmaster@<dominio>` (empezar en `p=none`; endurecer tras ver reportes) |

DKIM: se activa en el webmail entrando como `postmaster` (*Domain Settings*). Con la opción A el
registro se publica solo; con la B, copia el TXT que muestre el webmail.

### A.4 Sitios, bindings y certificados

En el panel de MonsterASP:

1. **Sitio principal** → dominio `<dominio>` (y alias `www.<dominio>`). Sube solo
   `despliegue/raiz/index.html` de este repositorio (portada estática), después de reemplazar
   `{{DOMINIO}}` por el dominio real.
2. **Subdomain website `altares`** → `altares.<dominio>`. Runtime **.NET 10**, modelo
   **InProcess**.
3. **Subdomain website `catrinas`** → `catrinas.<dominio>`. Igual que el anterior.
4. En cada uno de los tres: **HTTPS → Let's Encrypt → Enable** (Premium lo renueva solo; revisa
   que quede "auto-renew"). Activa también **Force HTTPS** si el panel lo ofrece; la app ya
   redirige y manda HSTS, pero así el primer salto ni siquiera llega en claro.
5. En cada sitio de app, revisa la **arquitectura del app pool** (32 o 64 bits). El paquete se
   genera para `win-x86` por defecto porque así funcionaba MonsterASP en 2026-09; si el panel dice
   64 bits, genera el paquete con `-Runtime win-x64` (sección A.8). Con la arquitectura
   equivocada el sitio responde `HTTP 500.30`.

### A.5 Bases de datos (dos, independientes)

**Databases → Create database → MSSQL**, dos veces:

| Base | Para | Nunca |
|---|---|---|
| `<base Altares>` | solo `Altares.Web` | la usa Catrinas |
| `<base Catrinas>` | solo `Catrinas.Web` | la usa Altares |

- Anota servidor, base, usuario y contraseña **en tu gestor de contraseñas**, no en archivos.
- La cadena de conexión tiene la forma
  `Server=<servidor>;Database=<base>;User Id=<usuario>;Password=<contraseña>;TrustServerCertificate=True;MultipleActiveResultSets=true`.
- **No hace falta correr migraciones a mano:** cada app aplica sus migraciones al arrancar contra
  una base vacía (probado en F4 con bases nuevas en LocalDB, ambas apps desde el paquete publicado).
- **Backups:** confirma en *Databases → (base) → Backups* que el respaldo diario está activo para
  las dos bases. La prueba de restauración está en [08-rollback-y-recuperacion.md](08-rollback-y-recuperacion.md).

### A.6 Google OAuth (dos clientes, uno por app)

En [Google Cloud Console](https://console.cloud.google.com) crea **dos proyectos** (o al menos
dos clientes OAuth), uno por plataforma, para que la pantalla de consentimiento muestre el nombre
correcto y revocar uno no afecte al otro:

| | Altares | Catrinas |
|---|---|---|
| Tipo de cliente | Aplicación web | Aplicación web |
| Orígenes JavaScript autorizados | `https://altares.<dominio>` | `https://catrinas.<dominio>` |
| URI de redirección autorizado | `https://altares.<dominio>/Registro/google-callback` | `https://catrinas.<dominio>/Registro/google-callback` |
| Alcances | `openid`, `email`, `profile` (no sensibles) | igual |

Pantalla de consentimiento:
- Si creas el proyecto con tu cuenta `@uabc.edu.mx` y la organización de Google Workspace de la
  UABC lo permite, elige **Interno**: solo entran cuentas de la organización (defensa adicional a
  la validación `hd` que ya hace la app).
- Si debe ser **Externo**, publícalo ("En producción"); en modo "Prueba" solo entran los usuarios
  de prueba listados. Con alcances no sensibles no requiere verificación de Google.
- No registres otros URI de redirección (ni `localhost`) en los clientes de producción.

### A.7 Buzones de correo

En el panel: **Email → Create mailbox** → `altares@<dominio>` y `catrinas@<dominio>` (contraseñas
distintas, en el gestor de contraseñas). El servidor SMTP es `mailXXX.mailasp.net`, puerto
**587** con STARTTLS (`System.Net.Mail` no soporta el 465). Límite: 300 correos por hora por
buzón. Activa DKIM (A.3).

### A.8 Generar los paquetes

Desde la raíz del repositorio, con el commit a desplegar ya hecho:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Publicar/publicar.ps1
```

Deja `Altares-<commit>-win-x86.zip` y `Catrinas-<commit>-win-x86.zip` en `..\publicacion\`
(fuera del repo). El script **falla** si el paquete trae algún `appsettings*.json`, si falta
`wwwroot`, si `web.config` no apunta al ejecutable correcto o si el GC no es el de estación de
trabajo. Los paquetes **no contienen configuración ni secretos**.

---

## B. Altares

### B.1 Variables de entorno del sitio (configuración y secretos)

Se definen en **el sitio de Altares → Scripting → Environment variables → Add**, y luego
**Restart application**. No se guardan en los archivos desplegados, sobreviven a cada nuevo
despliegue y solo las ve ese sitio. Sustituyen a `appsettings.json` (que **no** se sube).

| Variable | Valor | ¿Secreto? |
|---|---|---|
| `ConnectionStrings__AltarContext` | cadena de `<base Altares>` (A.5) | **Sí** |
| `GoogleAuth__ClientId` | ClientId del cliente OAuth **de Altares** | No (pero no se publica) |
| `GoogleAuth__ClientSecret` | ClientSecret del cliente OAuth de Altares | **Sí** |
| `GoogleAuth__ExigirDominioWorkspace` | `true` (no cambiar sin el procedimiento R7) | No |
| `GoogleAuth__DominioWorkspace` | `uabc.edu.mx` | No |
| `Smtp__Host` | `mailXXX.mailasp.net` | No |
| `Smtp__Port` | `587` | No |
| `Smtp__EnableSsl` | `true` | No |
| `Smtp__User` | `altares@<dominio>` | No |
| `Smtp__Password` | contraseña del buzón `altares@` | **Sí** |
| `Smtp__FromName` | `Concurso de Altares FIM` | No |
| `Smtp__TimeoutSegundos` | `30` | No |
| `Seguridad__CookieSesion` | `.Altares.Sesion` | No |
| `Seguridad__DataProtection__ApplicationName` | `Altares.Web` | No |
| `Seguridad__DataProtection__KeysPath` | `App_Data/keys` (relativo a la carpeta del sitio; no se sirve por web) | No |
| `Seguridad__RateLimiting__CuentaMaxFallidos` | `5` | No |
| `Seguridad__RateLimiting__CuentaVentanaMinutos` | `5` | No |
| `Seguridad__RateLimiting__IpLoginPorMinuto` | `300` | No |
| `Seguridad__RateLimiting__IpOAuthPorMinuto` | `600` | No |
| `Logging__LogLevel__Default` | `Information` | No |
| `Logging__LogLevel__Microsoft.AspNetCore` | `Warning` | No |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | `Warning` (en `Information` registra cada SQL) | No |
| `AllowedHosts` | `altares.<dominio>` | No |
| `SEED_ADMIN_USUARIO` | tu usuario de admin (si no, `abram`) | No |
| `SEED_ADMIN_PASSWORD` | contraseña **temporal** larga | **Sí — se borra tras el primer login** |

Notas:
- **No** definas `Seguridad__ForwardedHeaders__*` hasta cerrar el gate R6 (B.5).
- Si falta `GoogleAuth__ClientId` o `GoogleAuth__ClientSecret`, la app **no arranca** y el error
  dice qué falta (antes respondía 500 en todo el sitio).
- Las llaves de Data Protection quedan en `App_Data/keys` del sitio. **No borres esa carpeta al
  redesplegar:** si se pierde, se cierran todas las sesiones abiertas (no se pierden datos).

### B.2 Primer despliegue

1. Sitio de Altares → **File Manager** (o FTP / Web Deploy del panel) → sube **el contenido** de
   `Altares-<commit>-win-x86.zip` a la raíz del sitio (`Altares.Web.dll` y `web.config` en la raíz,
   no dentro de una subcarpeta).
2. Define las variables de B.1 y pulsa **Restart application**.
3. Abre `https://altares.<dominio>`. El primer arranque crea el esquema (migraciones), los
   catálogos y el admin semilla.
4. Pestaña **Jueces / Admin** → `SEED_ADMIN_USUARIO` + la contraseña temporal. La app obliga a
   cambiarla antes de cualquier otra cosa.
5. **Borra `SEED_ADMIN_PASSWORD`** de las variables del sitio y reinicia la app.

### B.3 Verificación inmediata (con datos ficticios)

- [ ] `http://altares.<dominio>` redirige a `https://` y responde con HSTS.
- [ ] Login de admin, cambio obligatorio de contraseña.
- [ ] `/AltarAdmin/Diagnostico` (solo admin) devuelve JSON; sirve para R6 y para vigilar memoria.
- [ ] Crear un participante de prueba, equipo, ficha, integrantes y Narrador; evaluación
      Preliminar y Final; constancias PDF y ZIP; Reporte de Cierre.

### B.4 Actualizar la app (despliegues siguientes)

1. Sube un archivo `app_offline.htm` a la raíz del sitio (la app se detiene y suelta los DLL; con
   InProcess, IIS los bloquea).
2. Sube el contenido del nuevo ZIP **sin borrar** `App_Data/` ni `wwwroot/uploads/` (recorrido PDF).
3. Borra `app_offline.htm`.

Las variables de entorno no se tocan. Desplegar Altares no obliga a desplegar Catrinas.

### B.5 Gate R6 — cabeceras reenviadas (en el sitio real)

**No se cierra porque HTTPS funcione.** Procedimiento:

1. Averigua tu IP pública (p. ej. buscando "what is my ip").
2. Entra como admin y abre `https://altares.<dominio>/AltarAdmin/Diagnostico`. Anota:
   `red.remoteIpAddress`, `red.scheme`, `red.isHttps` y las `cabecera.*` presentes.
3. Interpretación:

| Lo que ves | Significa | Acción |
|---|---|---|
| `remoteIpAddress` = tu IP, `scheme` = `https`, sin `cabecera.X-Forwarded-For` | IIS recibe la conexión directo (lo esperado con InProcess) | No configurar proxies. Pasar al paso 4. |
| `remoteIpAddress` = una IP del proveedor (p. ej. `10.x.x.x`) y **sí** hay `X-Forwarded-For` con tu IP | Hay un proxy delante | Definir `Seguridad__ForwardedHeaders__KnownProxies__0` = esa IP (y `__1`, … si ves varias tras varias recargas). Reiniciar y repetir el paso 2: ahora `remoteIpAddress` debe ser tu IP. |
| La IP del proxy cambia entre recargas dentro de una red privada | Varios proxies sin lista publicada | Pedir a soporte de MonsterASP la red de sus proxies y usar `KnownNetworks__0` = esa red. **No** usar `ConfiarEnCualquierOrigen`. Si no la dan, documentar que todo el tráfico comparte el techo por IP y decidir con el propietario (ver 07). |
| `scheme` = `http` aunque entraste por `https` | TLS termina antes de la app y no llega `X-Forwarded-Proto` confiable | Igual que la fila del proxy; sin eso las cookies `Secure` y el callback de Google fallarían. |

4. **Prueba de suplantación** (una cabecera enviada por un cliente nunca debe ganar):
   - En el navegador, con la sesión de admin abierta: DevTools → Application → Cookies → copia el
     valor de `.Altares.Sesion`. Es tu propia sesión; no lo pegues en ningún chat ni documento.
   - En PowerShell:
     `curl.exe -s -H "Cookie: .Altares.Sesion=<valor>" -H "X-Forwarded-For: 203.0.113.9" -H "X-Forwarded-Proto: http" https://altares.<dominio>/AltarAdmin/Diagnostico`
   - Esperado: `red.remoteIpAddress` sigue siendo **tu IP real**, `red.scheme` sigue en `https`, y
     la cabecera falsa solo aparece como `cabecera.X-Forwarded-For`. Si `remoteIpAddress` muestra
     `203.0.113.9`, **R6 falla**: revisa la configuración de proxies antes de seguir.
   - Cierra sesión al terminar (invalida el valor copiado).
5. Confirma además: login (cookie `Secure`), login con Google (R7) y un 429 de prueba del
   limitador por cuenta (6 intentos fallidos con un usuario de prueba → el 6.º responde 429 y la
   página lo explica).
6. Registra el resultado (sin IPs de usuarios reales) en [07-checklist-produccion.md](07-checklist-produccion.md).

### B.6 Gate R7 — Google con cuenta UABC real

1. En `https://altares.<dominio>` → "Continuar con Google" con una cuenta `@uabc.edu.mx` real.
2. **Si entra** (o llega a completar el registro): Google entregó `email_verified=true`,
   `hd=uabc.edu.mx` y el callback correcto. La app **no** guarda ni registra tokens ni claims.
3. **Si la rechaza** con el mensaje "Google no identificó tu cuenta @uabc.edu.mx como cuenta de la organización UABC … (código: hd)": **detenerse**. No cambiar
   `GoogleAuth__ExigirDominioWorkspace`. Reportar y decidir el fallback (ver 07, R7).
4. Con esa sesión abierta, abre `https://catrinas.<dominio>`: **no** debe aparecer sesionado
   (sesiones no compartidas). Repite el paso 1 en Catrinas: la pantalla de Google debe mostrar el
   nombre del proyecto de Catrinas (cliente correcto).

### B.7 Correo

Con un equipo de prueba cuyos correos sean buzones reales que puedas revisar (p. ej. tu cuenta
`@uabc.edu.mx`), y una evaluación Final:
- [ ] **Enviar constancias por correo** (Detalle de la evaluación): llegan la grupal, las
      individuales, la del maestro y la del juez (si el juez tiene correo), con remitente
      `Concurso de Altares FIM <altares@<dominio>>`, y **no** a spam.
- [ ] El ZIP de constancias individuales se descarga (el ZIP no se envía por correo).
- [ ] **Error SMTP:** cambia temporalmente `Smtp__Password` por un valor erróneo, reinicia y
      vuelve a enviar: la app regresa al detalle con el mensaje "No se pudieron enviar todas las
      constancias…" (no 500). Restaura la contraseña.
- [ ] **Timeout:** cubierto por pruebas automáticas (`CorreoSmtpTests`): un servidor que no
      responde se corta a los `Smtp__TimeoutSegundos`.
- [ ] Revisa las cabeceras del correo recibido: `spf=pass` y `dkim=pass`.

**Fallback:** Gmail con contraseña de aplicación (`Smtp__Host=smtp.gmail.com`, puerto 587). Las
constancias siempre se pueden descargar aunque el correo falle.

### B.8 Diagnóstico de errores

- Error al arrancar (500.30/500.31): activa temporalmente `stdoutLogEnabled="true"` en
  `web.config`, reproduce, lee `logs/stdout*.log` y **vuelve a apagarlo** (crece sin límite).
- Los logs nunca contienen contraseñas, tokens ni la dirección de destinatarios de correo.
