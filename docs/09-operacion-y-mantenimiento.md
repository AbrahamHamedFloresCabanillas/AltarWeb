# 09 — Operación y mantenimiento

> Fecha: 2026-10-17. Para el día a día de las dos plataformas ya desplegadas
> ([04](04-despliegue-altares.md), [05](05-despliegue-catrinas.md)).

## 1. Antes de cada concurso (cada periodo)

1. En cada app, **Configuración** del periodo: fechas límite, pesos (Catrinas: deben sumar 100 %),
   umbral de privacidad (≥ 3).
2. Crear o reactivar cuentas del jurado; al menos **dos admins por app** (ver §4).
3. Respaldo manual de ambas bases ([08 §3](08-rollback-y-recuperacion.md)).
4. Verificar que no quedan datos ficticios: `despliegue/verificar-limpieza-*.sql` en cero.
5. Revisar que el certificado HTTPS de los tres sitios está vigente y en renovación automática.

## 2. Durante el concurso

- **Salud:** `/AltarAdmin/Diagnostico` y `/Admin/Diagnostico` (solo admin) muestran memoria,
  hilos, colecciones del GC y hora de inicio del proceso. Si `proceso.inicioUtc` cambia sin un
  despliegue, el proceso se reinició (posible falta de memoria): revisar el panel y considerar el
  complemento de RAM ([06](06-costos-infraestructura.md)).
- **429 legítimos:** ver [08 §5](08-rollback-y-recuperacion.md). Ajustar solo el techo por IP.
- **Correo:** el límite es 300 envíos por hora por buzón. "Enviar constancias" manda unas 10 por
  equipo; no enviar todos los equipos de golpe. Si el correo falla, las constancias se descargan.
- **Empates de Catrinas:** comparten lugar (1, 1, 3); el desempate lo decide el comité.

## 3. Desplegar una corrección

1. Commit en la rama correspondiente; pruebas en verde (`dotnet test` de las tres suites).
2. `tools/Publicar/publicar.ps1` (paquetes fuera del repo, verificados).
3. Si trae migraciones: respaldo manual de esa base.
4. `app_offline.htm` → subir → quitar `app_offline.htm` (04 §B.4). Conservar el ZIP anterior.
5. Desplegar una app no obliga a desplegar la otra, salvo que el cambio esté en `Concurso.Core` y
   se quiera en ambas.

## 4. Cuentas y secretos

- **Rotar un secreto** (contraseña de base, de buzón o ClientSecret de Google): cambiarlo en el
  proveedor, actualizar la variable de entorno del sitio y *Restart application*. No hay secretos
  en archivos ni en git.
- **Restablecer un admin sin otro admin disponible:** definir temporalmente en el sitio
  `SEED_ADMIN_USUARIO` con un usuario **nuevo** y `SEED_ADMIN_PASSWORD`; la semilla solo crea
  admin cuando la tabla de jueces está vacía, así que en una base con datos **no** sirve. En ese
  caso, desde el administrador SQL del panel, cambiar el `Rol` de una cuenta existente de juez a
  `Admin` y que esa persona restablezca la contraseña perdida (Altares) o cree una cuenta nueva
  (Catrinas). Evitar llegar aquí: dos admins por app.
- **Data Protection:** las llaves viven en `App_Data/keys` de cada sitio (sin cifrar en disco; el
  hosting compartido no ofrece DPAPI de máquina utilizable). No copiarlas entre sitios.

## 5. Después del concurso

- Respaldo final de ambas bases (se guarda en un lugar privado; contiene datos personales).
- Reporte de Cierre de Altares (PDF confidencial).
- Pendientes registrados para revisar (ADR §13 y auditoría):
  - **M1:** alta de integrantes por identificador sin consentimiento (riesgo aceptado para
    noviembre): invitación con aceptación o permitir salir de un equipo.
  - Regla de desempate de Catrinas (comité).
  - Migrar Altares a la plantilla PDF común (Lato) de `Concurso.Core`.
  - Design system compartido (Razor Class Library).
  - Edición administrativa de inscripciones en Catrinas y restablecimiento de contraseña del jurado.
  - Eliminar las tablas y columnas legadas de Catrina en Altares (migración separada, con respaldo).
  - Revisar retención de datos personales según el aviso de privacidad.

## 6. Monitoreo de dependencias

Una vez al mes y antes de cada despliegue:

```bash
dotnet list AltarWeb.sln package --vulnerable --include-transitive
```

Las alertas que solo afectan paquetes de Debug (*scaffolding*) no llegan al paquete publicado.
