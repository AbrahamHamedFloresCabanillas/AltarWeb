# 00 — Auditoría del estado actual

> Fecha: 2026-10-17. Comparación entre el punto de partida (tag `pre-separacion`, `6c800ae`) y la
> rama `separacion-altares-catrinas` al cerrar el trabajo de F4 que no depende del hosting.
> Fuentes: el código, las pruebas y [ADR-001](01-decision-arquitectura.md).

## 1. Punto de partida (auditoría de F0A)

- Monolito ASP.NET Core MVC **8** (fin de soporte 2026-11-10): 5 controladores, ~7.5k líneas de
  C#, ~4.5k de Razor, 23 migraciones, **sin pruebas**.
- Catrina era un apéndice de la evaluación del altar (misma pantalla, mismo POST, mismo juez y
  estado; sin inscripción, Personificador, ranking ni constancias propios).
- Seguridad: 22 hallazgos de la auditoría de julio ([`AUDITORIA_SEGURIDAD.md`](../AUDITORIA_SEGURIDAD.md)),
  la mayoría remediados. Abiertos o débiles al iniciar: límite de login de 10/min por IP
  (incompatible con el NAT de la FIM), ForwardedHeaders aceptando cualquier origen, login con
  Google sin exigir `email_verified`/`hd`.
- Despliegue documentado solo para hosting gratuito (`DESPLIEGUE.md`), con ajustes manuales tras
  cada publish y la configuración (donde van las credenciales) dentro del paquete.

## 2. Estado actual

| Área | Estado |
|---|---|
| Runtime | .NET 10 (LTS), EF Core / ASP.NET Core 10.0.13 |
| Estructura | Monorepo: `src/Altares.Web`, `src/Catrinas.Web`, `src/Concurso.Core` (solo infraestructura transversal, sin reglas de negocio), tres suites en `tests/`, herramientas en `tools/` |
| Altares | Concurso de altares completo, sin Catrina (esquema legado conservado), `CodigoPublico` estable |
| Catrinas | Plataforma independiente: inscripción (ligada a un altar por referencia blanda o independiente), integrantes, Personificador, jurado propio, Preliminar/Final con confirmación y concurrencia optimista, ranking por carrera con empates, constancias, administración |
| Independencia | Bases, sitios, cookies, Data Protection, clientes OAuth, buzones y paquetes separados; ninguna app lee la base de la otra |
| Seguridad | Ver sección 3 |
| Pruebas | 199 (Core 54, Altares 77, Catrinas 68), todas en verde |
| Carga | Línea base local de 200 usuarios: 0 errores tras corregir los defectos que encontró ([07 §3](07-checklist-produccion.md)) |
| Despliegue | Paquetes `win-x86` sin configuración, generados y verificados por script; secretos solo en variables de entorno del sitio |

## 3. Seguridad (resumen; detalle en la auditoría)

**Cerrado durante la separación:**
- SEC-04 → límite estricto por cuenta (5 fallidos / 5 min) + techo amplio por IP, configurable;
  verificado con 200 logins desde una IP y con escenarios de abuso.
- SEC-01 endurecido: `email_verified` y `hd=uabc.edu.mx` exigidos del lado del servidor.
- M2: una cuenta desactivada pierde su sesión abierta (ambas apps).
- F4: enumeración de cuentas por tiempo de respuesta corregida; `rowVersion` alterado y
  cierres Final simultáneos ya no producen 500; errores SMTP manejados sin registrar destinatarios;
  configuración con credenciales ya no viaja en el paquete; la app no arranca sin credenciales de
  Google en vez de fallar en cada petición.

**Abierto (con dueño y procedimiento):**
- **R6 / CFG-02:** cómo entrega MonsterASP la IP y el esquema. Se cierra solo en el hosting
  (04 §B.5).
- **R7:** que Google entregue `hd` para cuentas UABC reales (04 §B.6).
- **M1 (riesgo aceptado para noviembre):** alta de integrantes por identificador sin
  consentimiento; mitigación mínima en Catrinas. Revisión post-concurso.
- **Gate de privacidad (ADR §8.4):** datacenter, DPA y aviso de privacidad antes de datos reales.
- Deuda conocida de Altares: el recorrido PDF es accesible por URL directa sin sesión (hallazgo E).

## 4. Deuda técnica relevante

Design system duplicado entre apps; Altares con plantilla PDF propia (Arial); sin edición
administrativa de inscripciones ni restablecimiento de contraseña del jurado en Catrinas; tablas
legadas de Catrina en Altares; regla de desempate de Catrinas pendiente del comité. Lista completa
y prioridad en [09 §5](09-operacion-y-mantenimiento.md) y ADR §13.
