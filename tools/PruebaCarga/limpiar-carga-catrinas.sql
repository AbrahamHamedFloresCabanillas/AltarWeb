-- =====================================================================================
--  Catrinas - Borra SOLO los datos ficticios de la prueba de carga (tools/PruebaCarga):
--  inscripciones 'Carga NNN', cuentas 'carga.*@uabc.edu.mx' y jueces 'carga-juez-*'.
--  Correr contra la base de STAGING con:  sqlcmd -I -f 65001 -S <servidor> -d <base> -i limpiar-carga-catrinas.sql
--  No toca ningun otro registro ni la base de Altares.
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Los indices filtrados de Catrinas exigen QUOTED_IDENTIFIER ON.
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @ins TABLE (Id int);
INSERT INTO @ins SELECT Id FROM Inscripciones WHERE Nombre LIKE N'Carga [0-9][0-9][0-9]';

DELETE ie FROM IntegrantesEvaluados ie JOIN Evaluaciones e ON e.Id = ie.EvaluacionId WHERE e.InscripcionId IN (SELECT Id FROM @ins);
DELETE FROM Evaluaciones        WHERE InscripcionId IN (SELECT Id FROM @ins);
DELETE FROM MiembrosInscripcion WHERE InscripcionId IN (SELECT Id FROM @ins);
DELETE FROM Inscripciones       WHERE Id IN (SELECT Id FROM @ins);
-- A1: invitaciones pendientes de estas cuentas (la FK hacia Registrantes es Restrict).
IF OBJECT_ID('InvitacionesInscripcion') IS NOT NULL
    DELETE x FROM InvitacionesInscripcion x JOIN Registrantes r ON r.Id = x.RegistranteId
    WHERE r.CorreoInstitucional LIKE 'carga.%@uabc.edu.mx';
DELETE FROM Registrantes WHERE CorreoInstitucional LIKE 'carga.%@uabc.edu.mx'
    AND NOT EXISTS (SELECT 1 FROM MiembrosInscripcion m WHERE m.RegistranteId = Registrantes.Id)
    AND NOT EXISTS (SELECT 1 FROM Inscripciones i WHERE i.ResponsableId = Registrantes.Id OR i.MaestroEncargadoId = Registrantes.Id);
DELETE FROM Jueces WHERE Usuario LIKE 'carga-juez-%'
    AND NOT EXISTS (SELECT 1 FROM Evaluaciones e WHERE e.JuezId = Jueces.Id);

COMMIT TRANSACTION;

SELECT 'Inscripciones de carga restantes' AS Dato, COUNT(*) AS Total FROM Inscripciones WHERE Nombre LIKE N'Carga [0-9][0-9][0-9]'
UNION ALL SELECT 'Cuentas de carga restantes', COUNT(*) FROM Registrantes WHERE CorreoInstitucional LIKE 'carga.%@uabc.edu.mx'
UNION ALL SELECT 'Jueces de carga restantes', COUNT(*) FROM Jueces WHERE Usuario LIKE 'carga-juez-%';
