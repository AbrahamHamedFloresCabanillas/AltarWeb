# AGENTS.md — guía para agentes (Claude Code, Codex, Cursor…) y para quien trabaje con uno

Monorepo de dos aplicaciones ASP.NET Core (.NET 10) **independientes**: **Altares** (`src/Altares.Web`) y
**Catrinas** (`src/Catrinas.Web`), con infraestructura común en `src/Concurso.Core` (sin reglas de negocio).
Índice completo en [README.md](README.md); decisiones en [docs/01-decision-arquitectura.md](docs/01-decision-arquitectura.md).

## 1. Si te piden "levanta el programa / déjalo up and running / quiero probarlo"

**Un solo comando** (Windows, PowerShell; tarda 1–3 min la primera vez, ~30 s después):

```powershell
powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1
```

Hace, en orden: comprueba requisitos → compila → crea las bases `AltaresLocal` y `CatrinasLocal` → arranca las
dos apps en segundo plano → carga cuentas y equipos **ficticios** → escribe las credenciales. Es idempotente:
si ya está todo arriba, solo lo verifica; si falla, el mensaje dice qué falta.

| Quiero… | Comando |
|---|---|
| Detener las dos apps | `… levantar-local.ps1 -Detener` |
| Empezar de cero (borra las dos bases locales y regenera credenciales) | `… levantar-local.ps1 -Reiniciar` |
| Levantar sin datos ficticios | `… levantar-local.ps1 -SinDatos` |
| Otro SQL Server (autenticación de Windows) | `… levantar-local.ps1 -Servidor "<servidor>"` |

**Resultado esperado:** Altares en **https://localhost:7185** y Catrinas en **https://localhost:7186**.
Verifica con `GET /` → 200 en ambos puertos antes de decir que está listo. Logs: `.local/logs/`.

**Entrégale al usuario:** las dos URLs, la ruta `.local/credenciales-locales.txt` (usuarios y contraseñas
generadas al azar, **solo de pruebas locales**; la carpeta `.local/` está en `.gitignore`) y el manual
[docs/10-manual-pruebas-locales.md](docs/10-manual-pruebas-locales.md) (pruebas guiadas por rol).
No pegues las contraseñas en commits, issues ni documentos; en el chat, preferible indicar el archivo.

**Avisos que debes dar al usuario:**
- El navegador mostrará "conexión no privada" (certificado de desarrollo de .NET): *Avanzado → Continuar a
  localhost*, o una vez `dotnet dev-certs https --trust`. **No uses `http://`**: la cookie de sesión es `Secure`.
- **No funcionan en local:** "Continuar con Google" (hay marcadores, no un cliente OAuth real) y el envío de
  constancias por correo (sin SMTP). Se validan en el hosting (gates R6/R7, `docs/07`).
- Si el navegador integrado de tu herramienta rechaza `https://localhost` con certificado no confiable, verifica
  por HTTP (`curl -k`) y deja la revisión visual al usuario.

**Requisitos (verificados solo en Windows):** SDK de .NET 10, SQL Server LocalDB (`sqllocaldb info`),
PowerShell 5.1+. En macOS/Linux no hay LocalDB: usa un SQL Server en Docker y adapta las cadenas de conexión
(no verificado; las pruebas automáticas también usan LocalDB).

**Problemas típicos:** puerto 7185/7186 ocupado por otro programa (ciérralo; el script lo detecta) · "No se pudo
conectar a SQL Server" (instala LocalDB) · existe una base pero no `.local/config.json` (usa `-Reiniciar`) ·
una app no responde (lee `.local/logs/<app>.err.log`).

## 2. Comandos habituales

```powershell
dotnet build AltarWeb.sln -c Release            # compila todo (0 warnings esperados)
dotnet test  AltarWeb.sln -c Release            # 199 pruebas; usan LocalDB desechable, no tocan las bases locales
powershell -ExecutionPolicy Bypass -File tools/Publicar/publicar.ps1   # paquetes de despliegue (fuera del repo)
```

Prueba de carga: `tools/PruebaCarga` (ver su README). Solo contra datos ficticios.

## 3. Reglas del proyecto (del propietario; no las rompas)

- **No hagas push, no compres ni contrates nada** sin autorización explícita del propietario. No hagas merge a `master`.
- **Cero secretos en git ni en documentación:** nada de contraseñas, `ClientSecret`, cadenas de conexión reales.
  En producción la configuración va en variables de entorno del sitio; los `appsettings.json` locales están ignorados.
- **Datos ficticios vs. reales:** nunca los mezcles. Antes de abrir el registro real, `despliegue/verificar-limpieza-*.sql` debe dar 0.
  No cargues datos reales hasta que el propietario cierre el gate de privacidad (ADR §8.4).
- **Seguridad:** no desactives la validación `hd` de Google, no relajes los límites de login (5 fallidos/5 min por
  cuenta) para "hacer pasar" pruebas, no cierres el gate R6 solo porque HTTPS funcione.
- **Independencia:** Altares y Catrinas no se leen entre sí; la referencia `ALT-…` es un dato blando. `Concurso.Core` no
  lleva reglas de negocio de ninguna de las dos.
- **Ritmo de trabajo:** plan → confirmar → implementar → verificar → limpiar. Una fase = un commit con pruebas en verde.
  Un defecto se reproduce con una prueba que falle **antes** de corregirlo.
- No borres código ni datos históricos sin demostrar que ya no hacen falta.

## 4. Estado y qué sigue (2026-10-20)

F0A–F3 aprobadas; F4 hecha hasta lo que exige el hosting real. **Pendiente (requiere al propietario):** contratar
MonsterASP Premium + dominio y seguir [docs/04](docs/04-despliegue-altares.md) y [docs/05](docs/05-despliegue-catrinas.md);
después los gates R6 y R7, correo real, carga en el hosting, independencia, aceptación manual y restauración de prueba.
Tracker: [docs/07-checklist-produccion.md](docs/07-checklist-produccion.md). Rama de trabajo:
`separacion-altares-catrinas`; punto de retorno: tag `pre-separacion`.
