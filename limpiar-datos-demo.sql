-- =====================================================================================
--  AltarWeb - Borra TODO lo que creo datos-demo.sql (periodo 2026-2).
--  No toca ningun otro periodo ni los registrantes reales: solo las filas cuyo correo
--  termina en '.demo@uabc.edu.mx' y los 4 equipos de demostracion por nombre.
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @periodo nvarchar(10) = '2026-2';

DECLARE @equipos TABLE (Id int);
INSERT INTO @equipos
SELECT Id FROM EquiposConcurso
WHERE Periodo = @periodo
  AND Nombre IN (N'Los Cempasúchil', N'Xolos del Mictlán', N'Flor de Muerto', N'Corazón de Copal');

DECLARE @evals TABLE (Id int);
INSERT INTO @evals SELECT Id FROM EvaluacionesConcurso WHERE EquipoId IN (SELECT Id FROM @equipos);

DELETE FROM ElementosEvaluados      WHERE EvaluacionId IN (SELECT Id FROM @evals);
DELETE FROM EvaluacionIntegrantes   WHERE EvaluacionId IN (SELECT Id FROM @evals);
DELETE FROM EvaluacionesCatrina     WHERE EvaluacionId IN (SELECT Id FROM @evals);
DELETE FROM EvaluacionesConcurso    WHERE Id IN (SELECT Id FROM @evals);
DELETE FROM RegistranteEquipos      WHERE EquipoId IN (SELECT Id FROM @equipos);
DELETE FROM Difuntos                WHERE EquipoId IN (SELECT Id FROM @equipos);
DELETE FROM EquiposConcurso         WHERE Id IN (SELECT Id FROM @equipos);
-- A1: las invitaciones pendientes de estas cuentas (la FK hacia Registrantes es Restrict).
IF OBJECT_ID('InvitacionesEquipo') IS NOT NULL
    DELETE i FROM InvitacionesEquipo i JOIN Registrantes r ON r.Id = i.RegistranteId
    WHERE r.CorreoInstitucional LIKE '%.demo@uabc.edu.mx';
DELETE FROM Registrantes            WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx';

-- Si el periodo quedo sin equipos, el consecutivo de CodigoPublico vuelve a empezar en 0001
-- (los equipos reales no heredan numeros de datos ficticios). Con equipos restantes no se toca.
DELETE c FROM ContadoresCodigoPublico c WHERE NOT EXISTS (SELECT 1 FROM EquiposConcurso e WHERE e.Periodo = c.Periodo);

COMMIT TRANSACTION;

SELECT 'Registrantes demo restantes' AS Concepto, COUNT(*) AS Total
FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
UNION ALL SELECT 'Equipos en ' + @periodo, COUNT(*) FROM EquiposConcurso WHERE Periodo = @periodo;
