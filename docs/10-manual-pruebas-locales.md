# 10 — Manual de pruebas locales (antes de contratar el hosting)

> Para que cualquiera del equipo use y revise las dos plataformas en su computadora, con datos ficticios.
> Complementa el checklist de [07](07-checklist-produccion.md). **Este documento no contiene contraseñas**:
> se generan al azar en cada máquina y quedan en `.local/credenciales-locales.txt` (ignorado por git).

## 1. Cómo arrancar (un comando)

**Con un agente:** abre el repo en Claude Code (o el agente que uses) y pídele: *"Lee AGENTS.md y levanta el
programa en local"*. Él ejecuta lo de abajo y te dice las URLs y dónde están las credenciales.

**A mano**, desde la raíz del repositorio (Windows, PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1
```

Requisitos: SDK de **.NET 10** y **SQL Server LocalDB** (viene con Visual Studio). La primera vez tarda 1–3 min.
Al terminar:

- **Altares:** https://localhost:7185 · **Catrinas:** https://localhost:7186
- Credenciales y cuentas: `.local/credenciales-locales.txt`
- Bases independientes: `AltaresLocal` y `CatrinasLocal` (LocalDB).

| Quiero… | Opción del script |
|---|---|
| Detener las dos apps | `-Detener` |
| Empezar de cero (borra las dos bases y regenera credenciales y datos) | `-Reiniciar` |
| Levantar sin cuentas ni equipos ficticios | `-SinDatos` |
| Ver errores | `.local/logs/altares.err.log`, `.local/logs/catrinas.err.log` |

**Aviso del navegador:** las apps usan HTTPS con el certificado de desarrollo de .NET, que Windows
no confía por defecto. Verás "su conexión no es privada": pulsa *Avanzado → Continuar a
localhost*. Si prefieres no verlo, ejecuta una vez `dotnet dev-certs https --trust` y acepta el
cuadro de Windows. **No uses `http://`**: la cookie de sesión es `Secure` y no se podría iniciar sesión.

**Qué NO funciona en local (y es normal):**
- **Continuar con Google:** necesita un cliente OAuth real y un dominio. Se prueba en el hosting (R7).
- **Enviar constancias por correo:** no hay SMTP configurado; verás un mensaje de que no se pudo
  enviar y las constancias se descargan igual. Se prueba en el hosting.

## 2. Cuentas de prueba ya cargadas

| Quién | Dónde entra | Usuario / correo | Notas |
|---|---|---|---|
| **Administrador** | Altares: pestaña *Jueces / Admin*. Catrinas: pestaña *Jurado / Admin* | `admin-local` | |
| **Jueces / jurado** (3) | igual que el admin | `carga-juez-1`, `carga-juez-2`, `carga-juez-3` | Rol juez (sin administración) |
| **Participantes** | Altares: pestaña *Registro*. Catrinas: pestaña *Participantes* | `carga.a1@uabc.edu.mx` … `carga.a5@uabc.edu.mx` | Cada uno es **responsable** de su equipo (`Carga 001`…`Carga 005`) |
| Integrantes | igual | `carga.b1@uabc.edu.mx` … `carga.b5@uabc.edu.mx` | Pertenecen al equipo del mismo número |
| Maestros (solo Altares) | igual | `carga.m1@uabc.edu.mx` … `carga.m5@uabc.edu.mx` | Maestro encargado del equipo del mismo número |

- Contraseñas: ver `.local/credenciales-locales.txt` (una para el admin y otra compartida por jueces y participantes).
- El equipo/inscripción **`Carga 001`** ya está evaluado como **Final** (sirve para ver ranking,
  constancias y reporte). Los equipos 2 a 5 están completos pero **sin evaluar**.
- Para probar **registro y alta de equipo desde cero** no uses estas cuentas (ya tienen equipo):
  regístrate con una nueva (sección 3, prueba A1).

## 3. Pruebas guiadas — Altares (https://localhost:7185)

Marca cada una. "Esperado" es lo que debe pasar; si no pasa, anótalo con la hora y la pantalla.

### A. Participante
- **A1. Registro nuevo.** Pestaña *Registro* → crear cuenta con: nombre inventado, **identificador de 7
  dígitos** (alumno), correo `lo.que.quieras@uabc.edu.mx`, contraseña de 6+ caracteres. *Esperado:* entras al
  panel. Prueba también errores: identificador de 6 dígitos, correo `@gmail.com`, correo repetido → mensajes claros.
- **A2. Crear equipo.** Panel → crear equipo: nombre del grupo, nombre del altar, carrera, **maestro encargado
  de 5 dígitos** (puedes usar `97001`, ya registrado, u otro número: queda "pendiente" hasta que se
  registre), difunto y fecha de defunción. *Esperado:* se crea, recibe un **código público**
  `ALT-AAAA-P-NNNN` y pasa a la Ficha.
- **A3. Ficha.** Completa lugar de exposición y revisa qué falta. *Esperado:* la Ficha dice qué campos faltan.
- **A4. Integrantes y Narrador.** Agrega a otro por su identificador (por ejemplo, otro registro que hayas
  hecho en A1 con otro navegador/ventana privada); designa **un** Narrador. *Esperado:* solo puede haber un
  Narrador; quien ya está en otro equipo no se puede agregar.
- **A5. Un registrante, un equipo.** Con `carga.a2@…`, intenta crear un segundo equipo. *Esperado:* lo rechaza.

### B. Juez
- **B1. Login.** Pestaña *Jueces / Admin* con `carga-juez-1`. Error a propósito: contraseña incorrecta
  **6 veces** → *Esperado:* a partir del 6.º intento, mensaje de "demasiados intentos" (HTTP 429) y
  **esperar 5 min o usar otro juez**.
- **B2. Evaluar Preliminar.** *Nueva evaluación* → buscar `Carga 002` → llenar checklist de elementos y los
  puntajes → guardar **Preliminar**. *Esperado:* queda guardado y se puede editar.
- **B3. Cerrar Final.** Mismo equipo → cerrar como **Final**. *Esperado:* se calcula la nota, el equipo
  entra al ranking de su carrera con lugar 1, 2 o 3 y la evaluación ya no se edita.
- **B4. Final bloqueado si falta algo.** Crea un equipo nuevo (A2) **sin Narrador** y evalúalo como Final →
  *Esperado:* rechazo con el motivo ("falta Narrador…").
- **B5. Constancias.** En el detalle de `Carga 001`: descargar **grupal**, **individuales (ZIP)**, **maestro** y
  **juez**. *Esperado:* PDFs correctos, con el lugar si aplica. *Enviar por correo* mostrará que no hay SMTP.

### C. Administración
- **C1.** *Jueces*: crear un juez nuevo (queda con cambio de contraseña obligatorio), desactivarlo y
  reactivarlo. *Esperado:* un juez desactivado **pierde su sesión abierta** en la siguiente acción.
- **C2.** *Registrantes y equipos*: ver ambas pestañas, desactivar un registrante, editar un equipo,
  validar una ficha.
- **C3.** *Configuración*: fechas límite del periodo. Pon una fecha de inscripción **pasada** y comprueba que un
  participante nuevo ya no puede crear equipo.
- **C4.** *Reporte de cierre*: ver y descargar el PDF. *Esperado:* marca "CONFIDENCIAL" y agrupación con umbral
  de privacidad (grupos de menos de 3 se ocultan).
- **C5.** `/AltarAdmin/Diagnostico` (como admin): muestra IP, esquema (`https`) y memoria. Sin sesión de admin no
  debe responder.

### D. Seguridad básica (rápida)
- **D1.** Sin iniciar sesión, abre `https://localhost:7185/AltarAdmin/Jueces` → te lleva al login.
- **D2.** Como participante, abre `/AltarAdmin/Jueces` → sin permiso.
- **D3.** Cierra sesión y usa el botón "atrás": no debe mostrar datos protegidos sin pedir login.

## 4. Pruebas guiadas — Catrinas (https://localhost:7186)

### E. Participante
- **E1. Registro nuevo.** Pestaña *Participantes* → registrarte (contraseña de **8+** caracteres). Prueba
  maestro/administrativo (5 dígitos) y alumno (7).
- **E2. Inscripción.** Crear inscripción: nombre, carrera, maestro encargado **opcional** (si lo pones, debe
  tener cuenta en Catrinas: p. ej. registra uno de 5 dígitos antes) y, opcionalmente, un código
  `ALT-AAAA-P-NNNN` de Altares (copia uno de un equipo de Altares). *Esperado:* se guarda aunque ese
  código no exista (es solo una referencia).
- **E3. Integrantes y Personificador.** Agrega integrantes por identificador (el navegador pide confirmar) y
  designa **una** persona que personifica. *Esperado:* un solo Personificador; no se puede agregar a quien
  ya está en otra inscripción (mensaje genérico, sin revelar quién).
- **E4. Una inscripción por persona y periodo**, igual que A5.

### F. Jurado
- **F1. Preliminar → Final.** `carga-juez-1` → *Evaluar* `Carga 002` → cinco criterios 0–10 → Preliminar →
  Final. *Esperado:* pide **confirmación explícita**; sin Personificador, la inscripción no cierra.
- **F2. Concurrencia.** Con **dos navegadores** (uno normal y uno privado) abre la misma evaluación con dos
  jueces; guarda en uno y luego intenta guardar en el otro. *Esperado:* el segundo recibe el aviso de que
  alguien más modificó la evaluación (no pisa el trabajo).
- **F3. Ranking.** Evalúa Final a 3 o 4 inscripciones de la misma carrera con notas distintas y una repetida.
  *Esperado:* lugares 1.º a 3.º; **notas iguales comparten lugar** (1, 1, 3); no se inventa desempate.
- **F4. Constancias.** Grupal, individuales (ZIP; indica quién es Personificador), maestro y jurado.

### G. Administración
- **G1.** *Jurado*: aprobar una solicitud (pide acceso desde "Solicita acceso"), crear cuenta, desactivar.
- **G2.** *Participantes*: desactivar a un participante con sesión abierta → pierde la sesión.
- **G3.** *Configuración*: los pesos deben sumar **100 %** (prueba 90 %: debe rechazar) y el umbral de privacidad
  no puede ser menor que 3.
- **G4.** `/Admin/Diagnostico` solo para admin.

### H. Independencia (lo que más importa de la separación)
- **H1.** Inicia sesión en Altares y abre Catrinas en la misma ventana: **no** debes aparecer sesionado en
  Catrinas (cookies distintas: `.Catrinas.Sesion` frente a la de Altares).
- **H2.** **Detén Catrinas** (cierra su ventana) y usa Altares con normalidad. Luego al revés.
- **H3.** Las dos usan bases distintas: en SSMS o con `sqlcmd` verás `AltaresLocal` con tablas de equipos
  y `CatrinasLocal` con tablas de inscripciones; ninguna tiene las tablas de la otra.
- **H4.** Una inscripción de Catrinas con un `ALT-…` inexistente se guarda igual (es dato blando).

## 5. Qué revisar con ojo de usuario (no solo "funciona")

- Textos y ortografía, mensajes de error entendibles, que no aparezca ningún "AltarWeb" donde debería decir
  Catrinas (y al revés).
- Que cada pantalla se vea bien en el **celular** (ancho de ~390 px: F12 → modo móvil) y en tema claro/oscuro.
- Que los PDF tengan los nombres, el lugar y la leyenda correctos.
- Qué datos ve cada rol: un participante no debe ver datos de otros; el reporte no debe mostrar grupos
  de menos de 3 personas.

## 6. Cómo anotar hallazgos

Una línea por hallazgo: **app · prueba (A3, F2…) · qué hiciste · qué esperabas · qué pasó · hora**. Con eso
se reproduce y se corrige. Lo que dependa del hosting (Google, correo real, memoria, cabeceras de proxy) se
deja para los gates R6/R7 de [07](07-checklist-produccion.md).

## 7. Empezar de cero

`powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1 -Reiniciar` — detiene las apps, borra
**solo** `AltaresLocal` y `CatrinasLocal`, regenera credenciales y vuelve a cargar los datos ficticios.

## 8. Pruebas automáticas (opcional)

`dotnet test AltarWeb.sln -c Release` (199 pruebas). Usan bases desechables propias y **no** tocan
`AltaresLocal` ni `CatrinasLocal`.
