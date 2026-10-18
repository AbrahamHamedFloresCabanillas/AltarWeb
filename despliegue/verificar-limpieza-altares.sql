-- =====================================================================================
--  Altares - Verifica que la base de PRODUCCION no tiene datos demo ni de prueba de carga
--  antes de abrir el registro (docs/03 §5, docs/07). Solo lee; no modifica nada.
--  Todas las filas deben dar 0. Si alguna no, correr el script de limpieza correspondiente:
--    demo  -> limpiar-datos-demo.sql (raiz del repo)
--    carga -> tools/PruebaCarga/limpiar-carga-altares.sql
--  sqlcmd -I -f 65001 -S <servidor> -d <base> -i despliegue/verificar-limpieza-altares.sql
-- =====================================================================================
SET NOCOUNT ON;
SELECT 'Registrantes demo (*.demo@uabc.edu.mx)' AS Revision, COUNT(*) AS Total FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
UNION ALL SELECT 'Equipos demo', COUNT(*) FROM EquiposConcurso WHERE Nombre IN (N'Los Cempasúchil', N'Xolos del Mictlán', N'Flor de Muerto', N'Corazón de Copal')
UNION ALL SELECT 'Registrantes de carga (carga.*@uabc.edu.mx)', COUNT(*) FROM Registrantes WHERE CorreoInstitucional LIKE 'carga.%@uabc.edu.mx'
UNION ALL SELECT 'Equipos de carga (Carga NNN)', COUNT(*) FROM EquiposConcurso WHERE Nombre LIKE N'Carga [0-9][0-9][0-9]'
UNION ALL SELECT 'Jueces de carga (carga-juez-*)', COUNT(*) FROM Jueces WHERE Usuario LIKE 'carga-juez-%'
UNION ALL SELECT 'Evaluaciones sin equipo (huerfanas)', COUNT(*) FROM EvaluacionesConcurso e WHERE NOT EXISTS (SELECT 1 FROM EquiposConcurso q WHERE q.Id = e.EquipoId);
-- Informativo (no tiene que ser 0): siguiente CodigoPublico por periodo.
SELECT Periodo, Ultimo + 1 AS SiguienteConsecutivo FROM ContadoresCodigoPublico;
