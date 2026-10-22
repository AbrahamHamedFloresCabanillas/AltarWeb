# 05 — Despliegue de Catrinas

> Fecha: 2026-10-17. Fase F4 del [ADR-001](01-decision-arquitectura.md).
> La infraestructura común (dominio, DNS, sitios, certificados, bases, clientes OAuth, buzones y
> generación de paquetes) está en la **parte A** de [04-despliegue-altares.md](04-despliegue-altares.md).
> Aquí solo va lo propio de Catrinas. Ningún secreto aparece en este documento ni en git.

## 1. Independencia respecto de Altares

Catrinas usa **su propio** sitio (`catrinas.<dominio>`), su propia base, su propio cliente OAuth,
su propio buzón, su propia cookie (`.Catrinas.Sesion`) y su propio `ApplicationName` de Data
Protection (`Catrinas.Web`). No lee la base de Altares: la referencia a un altar (`ALT-…`) es un
dato blando que la persona responsable escribe y que se guarda tal cual (ADR §2.6). Se despliega,
detiene o actualiza sin tocar Altares.

## 2. Variables de entorno del sitio

**Sitio de Catrinas → Scripting → Environment variables → Add** y luego **Restart application**.

| Variable | Valor | ¿Secreto? |
|---|---|---|
| `ConnectionStrings__CatrinasContext` | cadena de `<base Catrinas>` | **Sí** |
| `GoogleAuth__ClientId` | ClientId del cliente OAuth **de Catrinas** | No (no se publica) |
| `GoogleAuth__ClientSecret` | ClientSecret del cliente OAuth de Catrinas | **Sí** |
| `GoogleAuth__ExigirDominioWorkspace` | `true` | No |
| `GoogleAuth__DominioWorkspace` | `uabc.edu.mx` | No |
| `Smtp__Host` | `mailXXX.mailasp.net` | No |
| `Smtp__Port` | `587` | No |
| `Smtp__EnableSsl` | `true` | No |
| `Smtp__User` | `catrinas@<dominio>` | No |
| `Smtp__Password` | contraseña del buzón `catrinas@` | **Sí** |
| `Smtp__FromName` | `Concurso de Catrinas FIM` | No |
| `Smtp__TimeoutSegundos` | `30` | No |
| `Seguridad__CookieSesion` | `.Catrinas.Sesion` (es también el valor por defecto) | No |
| `Seguridad__DataProtection__ApplicationName` | `Catrinas.Web` (también por defecto) | No |
| `Seguridad__DataProtection__KeysPath` | `App_Data/keys` | No |
| `Seguridad__RateLimiting__CuentaMaxFallidos` | `5` | No |
| `Seguridad__RateLimiting__CuentaVentanaMinutos` | `5` | No |
| `Seguridad__RateLimiting__IpLoginPorMinuto` | `300` | No |
| `Seguridad__RateLimiting__IpOAuthPorMinuto` | `600` | No |
| `Logging__LogLevel__Default` | `Information` | No |
| `Logging__LogLevel__Microsoft.AspNetCore` | `Warning` | No |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | `Warning` | No |
| `AllowedHosts` | `catrinas.<dominio>` | No |
| `SEED_ADMIN_USUARIO` | tu usuario de admin (si no, `admin-catrinas`) | No |
| `SEED_ADMIN_PASSWORD` | contraseña **temporal** larga y **distinta** de la de Altares | **Sí — se borra tras el primer login** |
| `Legal__CorreoPrivacidad`, `Legal__Domicilio`, `Legal__PlazoConservacion` | correo para derechos ARCO, domicilio del responsable y plazo de conservación que defina la UABC (aviso de privacidad, 2026-10-22). Sin definir, el aviso muestra **[POR DEFINIR]** | No |

Igual que en Altares: no definas `Seguridad__ForwardedHeaders__*` hasta cerrar R6; sin
`GoogleAuth__ClientId/ClientSecret` la app no arranca y lo dice; no borres `App_Data/keys` al
redesplegar.

## 3. Primer despliegue

1. Sube el contenido de `Catrinas-<commit>-win-x86.zip` a la raíz del sitio de Catrinas.
2. Define las variables de la sección 2 y reinicia la app.
3. Abre `https://catrinas.<dominio>`: el primer arranque aplica la migración `InicialCatrinas`
   (que trae los catálogos: 13 carreras y géneros) y crea el admin semilla. La configuración del
   periodo usa los valores por defecto hasta que se guarde en **Configuración**.
4. Pestaña **Jurado** → admin semilla + contraseña temporal → cambio obligatorio.
5. Borra `SEED_ADMIN_PASSWORD` de las variables y reinicia.
6. En **Configuración**, revisa fechas límite y pesos del periodo antes de abrir inscripciones.

## 4. Verificación (con datos ficticios)

- [ ] `http://` redirige a `https://` con HSTS; cookie `.Catrinas.Sesion` marcada `Secure`.
- [ ] `/Admin/Diagnostico` (solo admin) responde JSON.
- [ ] Registro de participante, inscripción, integrantes, Personificador, referencia opcional a
      un altar (`ALT-AAAA-P-NNNN`), evaluación Preliminar y Final (con la confirmación), ranking,
      constancias (grupal, individuales en ZIP, maestro y jurado) y administración.

## 5. Gates R6, R7 y correo

Mismo procedimiento que [04 §B.5–B.7](04-despliegue-altares.md#b5-gate-r6--cabeceras-reenviadas-en-el-sitio-real),
cambiando:
- diagnóstico: `https://catrinas.<dominio>/Admin/Diagnostico`;
- cookie para la prueba de suplantación: `.Catrinas.Sesion`;
- remitente esperado: `Concurso de Catrinas FIM <catrinas@<dominio>>`;
- cliente OAuth: el de Catrinas (la pantalla de Google muestra el proyecto de Catrinas).

Catrinas sí envía la constancia del jurado por correo si el juez tiene correo registrado.

## 6. Actualizar la app

Igual que Altares (04 §B.4): `app_offline.htm` → subir el ZIP sin borrar `App_Data/` → quitar
`app_offline.htm`. No requiere tocar Altares.
