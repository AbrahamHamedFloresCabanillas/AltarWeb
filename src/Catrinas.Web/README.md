# Catrinas.Web — Concurso de Catrinas

Plataforma **independiente** del Concurso de Catrinas de la FIM (UABC). La decisión de arquitectura
y el modelo funcional acordado están en [`docs/01-decision-arquitectura.md`](../../docs/01-decision-arquitectura.md)
(§2 y §7.1).

**Recursos propios** (no se comparten con Altares):
- ejecutable;
- `CatrinasDbContext`;
- migraciones (`Datos/Migraciones`) y base SQL Server;
- configuración y secretos;
- cookies (`.Catrinas.Sesion`, `.Catrinas.Antiforgery`, `.Catrinas.Externa`);
- llaves de Data Protection (`ApplicationName = Catrinas.Web`);
- cliente OAuth en producción;
- admin semilla y configuración por periodo.

Solo depende de `Concurso.Core` (seguridad, OAuth base, privacidad, fechas, correo y componentes PDF).
No consulta la base de Altares, no llama a sus endpoints y funciona aunque Altares no exista.

## Ejecutar en local

1. **.NET 10 SDK** y SQL Server LocalDB.
2. Configuración: copia `appsettings.Ejemplo.json` como `appsettings.json` (git lo ignora) o usa variables
   de entorno (`ConnectionStrings__CatrinasContext`, `GoogleAuth__ClientId`, …).
3. **Admin semilla:** antes del primer arranque define `SEED_ADMIN_PASSWORD` (y opcionalmente
   `SEED_ADMIN_USUARIO`, por defecto `admin-catrinas`).
   - Si no la defines, se genera una contraseña aleatoria que se imprime **una sola vez** en el log.
   - En el primer login se exige cambiarla.
4. Arranca:
   ```bash
   dotnet run --project src/Catrinas.Web --urls "http://localhost:5186"
   ```
   El arranque aplica **solo** las migraciones de Catrinas sobre una base vacía (catálogos de 13 carreras
   y 5 géneros incluidos).
5. Datos ficticios opcionales (nunca en una base real): `Datos/Demo/datos-demo-catrinas.sql`, y para
   quitarlos `Datos/Demo/limpiar-datos-demo-catrinas.sql`. Ejecútalos con `sqlcmd ... -f 65001 -i <archivo>`.

## Qué hace

| Actor | Flujo |
|---|---|
| Participante (alumno 7 dígitos / maestro o administrativo 5 dígitos, correo `@uabc.edu.mx`) | Registro local o con Google (`email_verified` + `hd`). Crea **una inscripción por periodo** (equipo de 1 a N personas) como responsable, agrega integrantes, designa a **una** persona que personifica a la Catrina, maestro encargado opcional y, si quiere, el **código público de su equipo de Altares** (`ALT-AAAA-P-NNNN`) como referencia declarada, sin validación remota. Descarga su constancia cuando la evaluación es Final. |
| Jurado | Solicita acceso (queda pendiente) o lo crea la administración. Evalúa 5 criterios 0–10 (vestuario, maquillaje, accesorios, originalidad y creatividad, apego a las tradiciones): Preliminar editable → Final con confirmación explícita (exige un Personificador). Consulta el ranking por carrera y descarga o envía las constancias. |
| Administración | Aprueba o desactiva cuentas del jurado, gestiona participantes y configura el periodo: fechas límite, pesos (deben sumar 100 %) y umbral de privacidad (≥ 3). |

**Reglas clave:**
- **Nota:** suma ponderada con los pesos vigentes **al guardar**. Cada evaluación guarda los pesos con los que se calculó; cambiarlos después **no** recalcula lo ya guardado.
- **Ranking por carrera y periodo:** solo evaluaciones Final. **No hay criterio de desempate definido** (vision.md §13.11): notas iguales comparten lugar (1, 1, 3) y no se usa ningún campo incidental para desempatar.
- **Constancias:** grupal, individual (indica Personificador o Integrante, en ZIP), maestro encargado (si existe) y jurado, con el lugar cuando aplica. Se generan desde el snapshot de la evaluación.

## Pruebas

```bash
dotnet test tests/Catrinas.Tests
```

Son pruebas unitarias más HTTP contra la app real, con LocalDB desechable. Cubren:
- base vacía + migraciones;
- independencia de Altares;
- aislamiento de sesiones;
- registro, inscripción, evaluación, ranking y constancias reales;
- seguridad.
