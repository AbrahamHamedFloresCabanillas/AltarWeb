-- =====================================================================================
--  Concurso de Catrinas - Datos de demostracion (periodo 2026-2)
--  Ejecutar SOLO sobre una base de Catrinas ya migrada, NUNCA sobre una base con datos
--  reales del concurso. Independiente de Altares (no toca ni consulta su base).
--
--  Crea: 8 participantes ficticios (6 alumnos + 2 maestros), 3 inscripciones de Catrina con
--        Personificador designado, 1 evaluacion Final y 1 Preliminar, y su snapshot.
--
--  Todo lo ficticio lleva el sufijo '.demo@uabc.edu.mx' en el correo y matriculas del rango
--  1990xxx / 199xx, para borrarlo con limpiar-datos-demo-catrinas.sql. Es idempotente: si ya
--  existe, no duplica nada. Ejecutar con:  sqlcmd -S <servidor> -d <base> -f 65001 -i datos-demo-catrinas.sql
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Los indices filtrados de Catrinas exigen QUOTED_IDENTIFIER ON (sqlcmd lo deja en OFF por defecto).
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @periodo nvarchar(10) = '2026-2';
DECLARE @hoy datetime2 = SYSUTCDATETIME();
DECLARE @carrComputacion int = (SELECT Id FROM Carreras WHERE Nombre = N'Ing. en Computación');
DECLARE @carrCivil int = (SELECT Id FROM Carreras WHERE Nombre = N'Ing. Civil');

-- Participantes ficticios (sin contraseña: solo sirven para mostrar datos; no pueden iniciar sesion).
MERGE Registrantes AS destino
USING (VALUES
    (N'Alumno', N'Valeria Demo Catrina',   '1990001', 'valeria.catrina.demo@uabc.edu.mx'),
    (N'Alumno', N'Mateo Demo Catrina',     '1990002', 'mateo.catrina.demo@uabc.edu.mx'),
    (N'Alumno', N'Regina Demo Catrina',    '1990003', 'regina.catrina.demo@uabc.edu.mx'),
    (N'Alumno', N'Emiliano Demo Catrina',  '1990004', 'emiliano.catrina.demo@uabc.edu.mx'),
    (N'Alumno', N'Ximena Demo Catrina',    '1990005', 'ximena.catrina.demo@uabc.edu.mx'),
    (N'Alumno', N'Santiago Demo Catrina',  '1990006', 'santiago.catrina.demo@uabc.edu.mx'),
    (N'Maestro', N'Mtra. Lucía Demo',      '19901',   'lucia.maestra.demo@uabc.edu.mx'),
    (N'Maestro', N'Mtro. Ernesto Demo',    '19902',   'ernesto.maestro.demo@uabc.edu.mx')
) AS origen (Tipo, Nombre, Identificador, Correo)
    ON destino.Identificador = origen.Identificador
WHEN NOT MATCHED THEN
    INSERT (Tipo, NombreCompleto, Identificador, CorreoInstitucional, ProveedorAuth, Activo, CreadoEn)
    VALUES (origen.Tipo, origen.Nombre, origen.Identificador, origen.Correo, 'Demo', 1, @hoy);

DECLARE @r TABLE (Identificador varchar(7), Id int);
INSERT INTO @r SELECT Identificador, Id FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx';

-- Inscripciones (una ligada por referencia blanda a un equipo ficticio de Altares).
MERGE Inscripciones AS destino
USING (VALUES
    (N'La Catrina de Cempasúchil (demo)', @carrComputacion, '1990001', '19901', 'ALT-2026-2-0001', N'Los Cempasúchil'),
    (N'Dama del Mictlán (demo)',          @carrComputacion, '1990003', NULL,    NULL,              NULL),
    (N'Catrina Ingeniera (demo)',         @carrCivil,       '1990005', '19902', NULL,              NULL)
) AS origen (Nombre, CarreraId, Responsable, Maestro, AltarId, AltarNombre)
    ON destino.Nombre = origen.Nombre AND destino.Periodo = @periodo
WHEN NOT MATCHED THEN
    INSERT (Nombre, CarreraId, Periodo, ResponsableId, MaestroEncargadoId, AltarExternalId, AltarNombreSnapshot, Activo, CreadoEn)
    VALUES (origen.Nombre, origen.CarreraId, @periodo,
            (SELECT Id FROM @r WHERE Identificador = origen.Responsable),
            (SELECT Id FROM @r WHERE Identificador = origen.Maestro),
            origen.AltarId, origen.AltarNombre, 1, @hoy);

DECLARE @i TABLE (Nombre nvarchar(150), Id int);
INSERT INTO @i SELECT Nombre, Id FROM Inscripciones WHERE Periodo = @periodo AND Nombre LIKE N'%(demo)';

-- Integrantes: el primero de cada par es el responsable; el segundo personifica a la Catrina.
MERGE MiembrosInscripcion AS destino
USING (VALUES
    (N'La Catrina de Cempasúchil (demo)', '1990001', 'Integrante'),
    (N'La Catrina de Cempasúchil (demo)', '1990002', 'Personificador'),
    (N'Dama del Mictlán (demo)',          '1990003', 'Integrante'),
    (N'Dama del Mictlán (demo)',          '1990004', 'Personificador'),
    (N'Catrina Ingeniera (demo)',         '1990005', 'Personificador'),
    (N'Catrina Ingeniera (demo)',         '1990006', 'Integrante')
) AS origen (Inscripcion, Registrante, Rol)
    ON destino.RegistranteId = (SELECT Id FROM @r WHERE Identificador = origen.Registrante) AND destino.Periodo = @periodo
WHEN NOT MATCHED THEN
    INSERT (InscripcionId, RegistranteId, Rol, Periodo, FechaIngreso)
    VALUES ((SELECT Id FROM @i WHERE Nombre = origen.Inscripcion), (SELECT Id FROM @r WHERE Identificador = origen.Registrante), origen.Rol, @periodo, @hoy);

-- Evaluaciones: la nota usa los pesos por defecto (25/20/10/20/25), igual que CalculoCatrina.
DECLARE @eval TABLE (Inscripcion nvarchar(150), Estado nvarchar(20), V decimal(5,2), M decimal(5,2), A decimal(5,2), O decimal(5,2), T decimal(5,2));
INSERT INTO @eval VALUES
    (N'La Catrina de Cempasúchil (demo)', 'Final',      9.5, 9.0, 8.0, 9.5, 10.0),
    (N'Dama del Mictlán (demo)',          'Preliminar', 8.0, 8.5, 7.0, 8.0, 8.0);

INSERT INTO Evaluaciones (InscripcionId, JuezId, Periodo, Estado, Vestuario, Maquillaje, Accesorios, OriginalidadCreatividad, ApegoTradiciones,
    PesoVestuario, PesoMaquillaje, PesoAccesorios, PesoOriginalidadCreatividad, PesoApegoTradiciones, NotaCatrina, Lugar,
    SnapshotNombreInscripcion, SnapshotCarrera, SnapshotPersonificador, SnapshotMaestroEncargado, SnapshotAltarExternalId, SnapshotAltarNombre,
    CreadoEn, ActualizadoEn, FinalizadaEn)
SELECT ins.Id, NULL, @periodo, e.Estado, e.V, e.M, e.A, e.O, e.T,
       0.25, 0.20, 0.10, 0.20, 0.25,
       ROUND(e.V * 0.25 + e.M * 0.20 + e.A * 0.10 + e.O * 0.20 + e.T * 0.25, 2),
       CASE WHEN e.Estado = 'Final' THEN 1 END,
       ins.Nombre, c.Nombre, p.NombreCompleto, m.NombreCompleto, ins.AltarExternalId, ins.AltarNombreSnapshot,
       @hoy, @hoy, CASE WHEN e.Estado = 'Final' THEN @hoy END
FROM @eval e
JOIN Inscripciones ins ON ins.Nombre = e.Inscripcion AND ins.Periodo = @periodo
JOIN Carreras c ON c.Id = ins.CarreraId
JOIN MiembrosInscripcion mp ON mp.InscripcionId = ins.Id AND mp.Rol = 'Personificador'
JOIN Registrantes p ON p.Id = mp.RegistranteId
LEFT JOIN Registrantes m ON m.Id = ins.MaestroEncargadoId
WHERE NOT EXISTS (SELECT 1 FROM Evaluaciones x WHERE x.InscripcionId = ins.Id);

INSERT INTO IntegrantesEvaluados (EvaluacionId, RegistranteId, NombreCompleto, Identificador, CorreoInstitucional, Rol)
SELECT ev.Id, r.Id, r.NombreCompleto, r.Identificador, r.CorreoInstitucional, mi.Rol
FROM Evaluaciones ev
JOIN MiembrosInscripcion mi ON mi.InscripcionId = ev.InscripcionId
JOIN Registrantes r ON r.Id = mi.RegistranteId
WHERE ev.SnapshotNombreInscripcion LIKE N'%(demo)'
  AND NOT EXISTS (SELECT 1 FROM IntegrantesEvaluados x WHERE x.EvaluacionId = ev.Id AND x.RegistranteId = r.Id);

COMMIT TRANSACTION;

SELECT 'Participantes demo' AS Dato, COUNT(*) AS Total FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
UNION ALL SELECT 'Inscripciones demo', COUNT(*) FROM Inscripciones WHERE Nombre LIKE N'%(demo)'
UNION ALL SELECT 'Evaluaciones demo', COUNT(*) FROM Evaluaciones WHERE SnapshotNombreInscripcion LIKE N'%(demo)';
