-- =====================================================================================
--  Catrinas - Verifica que la base de PRODUCCION no tiene datos demo ni de prueba de carga
--  antes de abrir inscripciones (docs/03 §5, docs/07). Solo lee; no modifica nada.
--  Todas las filas deben dar 0. Si alguna no, correr:
--    demo  -> src/Catrinas.Web/Datos/Demo/limpiar-datos-demo-catrinas.sql
--    carga -> tools/PruebaCarga/limpiar-carga-catrinas.sql
--  sqlcmd -I -f 65001 -S <servidor> -d <base> -i despliegue/verificar-limpieza-catrinas.sql
-- =====================================================================================
SET NOCOUNT ON;
SELECT 'Registrantes demo (*.demo@uabc.edu.mx)' AS Revision, COUNT(*) AS Total FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
UNION ALL SELECT 'Inscripciones demo ((demo))', COUNT(*) FROM Inscripciones WHERE Nombre LIKE N'%(demo)'
UNION ALL SELECT 'Registrantes de carga (carga.*@uabc.edu.mx)', COUNT(*) FROM Registrantes WHERE CorreoInstitucional LIKE 'carga.%@uabc.edu.mx'
UNION ALL SELECT 'Inscripciones de carga (Carga NNN)', COUNT(*) FROM Inscripciones WHERE Nombre LIKE N'Carga [0-9][0-9][0-9]'
UNION ALL SELECT 'Jueces de carga (carga-juez-*)', COUNT(*) FROM Jueces WHERE Usuario LIKE 'carga-juez-%';
