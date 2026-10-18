-- =====================================================================================
--  Altares - Borra SOLO los datos ficticios de la prueba de carga (tools/PruebaCarga):
--  equipos 'Carga NNN', cuentas 'carga.*@uabc.edu.mx' y jueces 'carga-juez-*'.
--  Correr contra la base de STAGING con:  sqlcmd -I -f 65001 -S <servidor> -d <base> -i limpiar-carga-altares.sql
--  (o desde el administrador SQL del hosting). No toca ningun otro registro.
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @equipos TABLE (Id int);
INSERT INTO @equipos SELECT Id FROM EquiposConcurso WHERE Nombre LIKE N'Carga [0-9][0-9][0-9]';

DECLARE @evals TABLE (Id int);
INSERT INTO @evals SELECT Id FROM EvaluacionesConcurso WHERE EquipoId IN (SELECT Id FROM @equipos);

DELETE FROM ElementosEvaluados    WHERE EvaluacionId IN (SELECT Id FROM @evals);
DELETE FROM EvaluacionIntegrantes WHERE EvaluacionId IN (SELECT Id FROM @evals);
DELETE FROM EvaluacionesCatrina   WHERE EvaluacionId IN (SELECT Id FROM @evals);
DELETE FROM EvaluacionesConcurso  WHERE Id IN (SELECT Id FROM @evals);
DELETE FROM RegistranteEquipos    WHERE EquipoId IN (SELECT Id FROM @equipos);
DELETE FROM Difuntos              WHERE EquipoId IN (SELECT Id FROM @equipos);
DELETE FROM EquiposConcurso       WHERE Id IN (SELECT Id FROM @equipos);
DELETE FROM Registrantes          WHERE CorreoInstitucional LIKE 'carga.%@uabc.edu.mx'
    AND NOT EXISTS (SELECT 1 FROM RegistranteEquipos re WHERE re.RegistranteId = Registrantes.Id);
-- Un juez de carga que haya evaluado un equipo real no se borra (no deberia pasar en staging).
DELETE FROM Jueces WHERE Usuario LIKE 'carga-juez-%'
    AND NOT EXISTS (SELECT 1 FROM EvaluacionesConcurso e WHERE e.JuezId = Jueces.Id);

-- Si el periodo quedo sin equipos, el consecutivo de CodigoPublico vuelve a empezar en 0001
-- (los equipos reales no heredan numeros de datos ficticios). Con equipos restantes no se toca.
DELETE c FROM ContadoresCodigoPublico c WHERE NOT EXISTS (SELECT 1 FROM EquiposConcurso e WHERE e.Periodo = c.Periodo);

COMMIT TRANSACTION;

SELECT 'Equipos de carga restantes' AS Dato, COUNT(*) AS Total FROM EquiposConcurso WHERE Nombre LIKE N'Carga [0-9][0-9][0-9]'
UNION ALL SELECT 'Cuentas de carga restantes', COUNT(*) FROM Registrantes WHERE CorreoInstitucional LIKE 'carga.%@uabc.edu.mx'
UNION ALL SELECT 'Jueces de carga restantes', COUNT(*) FROM Jueces WHERE Usuario LIKE 'carga-juez-%';
