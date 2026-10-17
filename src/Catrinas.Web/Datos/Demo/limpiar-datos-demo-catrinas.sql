-- =====================================================================================
--  Concurso de Catrinas - Borra SOLO los datos de demostracion creados por
--  datos-demo-catrinas.sql (correos '.demo@uabc.edu.mx' e inscripciones '(demo)').
--  No toca ningun otro registro ni la base de Altares.
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Los indices filtrados de Catrinas exigen QUOTED_IDENTIFIER ON (sqlcmd lo deja en OFF por defecto).
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @ins TABLE (Id int);
INSERT INTO @ins SELECT Id FROM Inscripciones WHERE Nombre LIKE N'%(demo)';

DELETE ie FROM IntegrantesEvaluados ie JOIN Evaluaciones e ON e.Id = ie.EvaluacionId WHERE e.InscripcionId IN (SELECT Id FROM @ins);
DELETE FROM Evaluaciones WHERE InscripcionId IN (SELECT Id FROM @ins);
DELETE FROM MiembrosInscripcion WHERE InscripcionId IN (SELECT Id FROM @ins);
DELETE FROM Inscripciones WHERE Id IN (SELECT Id FROM @ins);
DELETE FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
    AND NOT EXISTS (SELECT 1 FROM MiembrosInscripcion m WHERE m.RegistranteId = Registrantes.Id)
    AND NOT EXISTS (SELECT 1 FROM Inscripciones i WHERE i.ResponsableId = Registrantes.Id OR i.MaestroEncargadoId = Registrantes.Id);

COMMIT TRANSACTION;

SELECT 'Participantes demo restantes' AS Dato, COUNT(*) AS Total FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
UNION ALL SELECT 'Inscripciones demo restantes', COUNT(*) FROM Inscripciones WHERE Nombre LIKE N'%(demo)';
