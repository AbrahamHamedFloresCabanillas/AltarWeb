# 06 — Costos de infraestructura

> Precios **[EXT]** consultados el 2026-10-17 en las páginas oficiales (detalle y fuentes en
> [ADR-001 §8](01-decision-arquitectura.md#8-hosting-costos-y-correo)). Tipo de cambio: DOF del
> 16-10-2026, 1 USD = 18.425 MXN. **Sin impuestos** (si aplica IVA, ×1.16; no se verificó si
> MonsterASP lo cobra).
>
> **Costos reales:** la tabla "Contratado" se llena con lo que muestre la factura al contratar. Hasta
> entonces son estimaciones.

## Recomendación vigente

| Concepto | Proveedor | Periodicidad | Estimado USD | Estimado MXN | Contratado (factura) |
|---|---|---|---:|---:|---|
| Hosting: Premium Single (1 sitio + 3 subdominios, 2 MSSQL de 2 GB, 512 MB RAM por sitio, backups diarios, Let's Encrypt, correo) | MonsterASP | anual, por adelantado | 23.40 (renueva a 30.00) | 431 | _pendiente_ |
| Dominio `.com` | Porkbun (referencia) | anual | ~11.08 | ~204 | _pendiente_ |
| Google OAuth (2 proyectos) | Google Cloud | — | 0 | 0 | — |
| Buzones `altares@` y `catrinas@` | incluidos en Premium | — | 0 | 0 | confirmar en el panel |
| **Total primer año** | | | **~34.48** | **~635** | _pendiente_ |

Ambas plataformas comparten el plan: separar en dos planes no aporta independencia real (cada app
ya tiene sitio, proceso, base, buzón y credenciales propios) y duplica el costo.

## Contingencias medidas o previstas

| Si… | Opción | Costo estimado |
|---|---|---|
| La prueba de carga en el hosting real supera 80 % de 512 MB o hay reciclajes | Complemento de RAM de MonsterASP | ~1 USD/mes por sitio (confirmar en el panel) |
| El cuello de botella es CPU o sigue fallando con más RAM | Plan B: Azure App Service Linux B1 + 2× Azure SQL Basic | ~23 USD el mes del concurso; ~275 USD/año 24/7 |
| El correo del hosting no entrega bien | Gmail con contraseña de aplicación | 0 |

**Dato de la línea base local (2026-10-17):** con GC de estación de trabajo y el heap limitado a
384 MB (75 % de 512 MB), la prueba más pesada (20 descargas concurrentes de PDF/ZIP) terminó sin
errores con un *working set* máximo de 315 MB. Indica que 512 MB probablemente alcanzan, pero la
decisión se toma con la prueba en el hosting real ([07](07-checklist-produccion.md)).

## Descartados

Planes gratuitos (MonsterASP Free, Azure F1, Supabase Free) para producción: RAM, CPU o cuotas
insuficientes para ~200 usuarios y generación de PDF (ADR §8.1).
