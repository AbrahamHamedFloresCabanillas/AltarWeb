-- =====================================================================================
--  AltarWeb - Datos de demostracion (periodo 2026-2)
--  Generado 2026-09-02. Ejecutar sobre una base ya migrada (con catalogos sembrados).
--
--  Crea: 18 registrantes (16 alumnos + 2 maestros), 4 equipos con Ficha completa,
--        3 evaluaciones (2 Final + 1 Preliminar), 1 Catrina, y los 24 elementos
--        evaluados de cada evaluacion.
--
--  Todo queda marcado con el sufijo '.demo@uabc.edu.mx' en el correo y matriculas del
--  rango 19xxxxx / 190xx, para poder borrarlo despues con limpiar-datos-demo.sql.
--
--  Las notas NO se escriben a mano: se calculan aqui con las mismas formulas del
--  sistema (vision.md 7.2 y 7.7), leyendo los pesos de ConfiguracionesPeriodo.
-- =====================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @periodo nvarchar(10) = '2026-2';
DECLARE @hoy datetime2 = SYSDATETIME();

-- -------------------------------------------------------------------------------------
-- 0. Configuracion del periodo (solo '2026-1' viene sembrado por la migracion)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM ConfiguracionesPeriodo WHERE Periodo = @periodo)
BEGIN
    INSERT INTO ConfiguracionesPeriodo
        (Periodo, FechaLimiteInscripcion, FechaLimiteRequisitos, RecorridoPdf,
         PesoObjetivoCultural, PesoEsenciaPersonalidad, PesoValoracionGeneral,
         PesoDistribucionNiveles, PesoNarrador, ValorSatisfaccionNoLoTiene,
         ValorSatisfaccionRegular, ValorSatisfaccionBueno,
         PesoElementoRitual, PesoElementoDecorativo, BonusPorElementoTematizado,
         ExigirMinimoUnAnioFallecimiento, UmbralAgrupacionDemografica,
         PesoCatrinaAccesorios, PesoCatrinaApegoTradiciones, PesoCatrinaMaquillaje,
         PesoCatrinaOriginalidadCreatividad, PesoCatrinaVestuario)
    SELECT TOP 1
         @periodo, NULL, NULL, NULL,
         PesoObjetivoCultural, PesoEsenciaPersonalidad, PesoValoracionGeneral,
         PesoDistribucionNiveles, PesoNarrador, ValorSatisfaccionNoLoTiene,
         ValorSatisfaccionRegular, ValorSatisfaccionBueno,
         PesoElementoRitual, PesoElementoDecorativo, BonusPorElementoTematizado,
         ExigirMinimoUnAnioFallecimiento, UmbralAgrupacionDemografica,
         PesoCatrinaAccesorios, PesoCatrinaApegoTradiciones, PesoCatrinaMaquillaje,
         PesoCatrinaOriginalidadCreatividad, PesoCatrinaVestuario
    FROM ConfiguracionesPeriodo
    ORDER BY Id;
END

DECLARE @cfgPesoOC decimal(5,4), @cfgPesoEP decimal(5,4), @cfgPesoVG decimal(5,4),
        @cfgPesoDN decimal(5,4), @cfgPesoNAR decimal(5,4), @cfgRitual decimal(5,4),
        @cfgDeco decimal(5,4), @cfgNoLoTiene decimal(5,4), @cfgRegular decimal(5,4),
        @cfgBueno decimal(5,4), @cfgBonus decimal(5,4),
        @cfgCatVest decimal(5,4), @cfgCatMaq decimal(5,4), @cfgCatAcc decimal(5,4),
        @cfgCatOrig decimal(5,4), @cfgCatTrad decimal(5,4);

SELECT @cfgPesoOC = PesoObjetivoCultural, @cfgPesoEP = PesoEsenciaPersonalidad,
       @cfgPesoVG = PesoValoracionGeneral, @cfgPesoDN = PesoDistribucionNiveles,
       @cfgPesoNAR = PesoNarrador, @cfgRitual = PesoElementoRitual,
       @cfgDeco = PesoElementoDecorativo, @cfgNoLoTiene = ValorSatisfaccionNoLoTiene,
       @cfgRegular = ValorSatisfaccionRegular, @cfgBueno = ValorSatisfaccionBueno,
       @cfgBonus = BonusPorElementoTematizado,
       @cfgCatVest = PesoCatrinaVestuario, @cfgCatMaq = PesoCatrinaMaquillaje,
       @cfgCatAcc = PesoCatrinaAccesorios, @cfgCatOrig = PesoCatrinaOriginalidadCreatividad,
       @cfgCatTrad = PesoCatrinaApegoTradiciones
FROM ConfiguracionesPeriodo WHERE Periodo = @periodo;

-- Catalogos (se resuelven por nombre, no por Id, para no depender del orden de siembra)
DECLARE @carrSistemas int = (SELECT Id FROM CatalogoCarreras WHERE Nombre = N'Lic. en Sistemas Computacionales');
DECLARE @carrComputacion int = (SELECT Id FROM CatalogoCarreras WHERE Nombre = N'Ing. en Computación');
DECLARE @carrIndustrial int = (SELECT Id FROM CatalogoCarreras WHERE Nombre = N'Ing. Industrial');
DECLARE @carrMecatronica int = (SELECT Id FROM CatalogoCarreras WHERE Nombre = N'Ing. en Mecatrónica');
DECLARE @genM int = (SELECT Id FROM CatalogoGeneros WHERE Nombre = N'Masculino');
DECLARE @genF int = (SELECT Id FROM CatalogoGeneros WHERE Nombre = N'Femenino');
DECLARE @genNB int = (SELECT Id FROM CatalogoGeneros WHERE Nombre = N'No binario');
DECLARE @genNE int = (SELECT Id FROM CatalogoGeneros WHERE Nombre = N'Prefiero no especificar');

-- Juez que "evalua" (el admin existente; si no hay ninguno, queda NULL)
DECLARE @juezId int = (SELECT TOP 1 Id FROM Jueces WHERE IsDeleted = 0 ORDER BY CASE WHEN Rol = 'Admin' THEN 0 ELSE 1 END, Id);

-- -------------------------------------------------------------------------------------
-- 1. Registrantes
-- -------------------------------------------------------------------------------------
DECLARE @reg TABLE (
    Rn int, Tipo nvarchar(20), Nombre nvarchar(200), Ident nvarchar(20), Correo nvarchar(200),
    Tel nvarchar(20), GenId int, CarrId int, Autodesc nvarchar(200));

INSERT INTO @reg (Rn, Tipo, Nombre, Ident, Correo, Tel, GenId, CarrId, Autodesc) VALUES
 (1 ,'Alumno', N'Ana Sofía Miranda Rocha',     '1900101','ana.miranda.demo@uabc.edu.mx',      '6861230001', @genF,  @carrComputacion, NULL),
 (2 ,'Alumno', N'Diego Alonso Rentería Vega',  '1900102','diego.renteria.demo@uabc.edu.mx',   '6861230002', @genM,  @carrComputacion, N'migrante'),
 (3 ,'Alumno', N'Karla Jimena Ontiveros Ruiz', '1900103','karla.ontiveros.demo@uabc.edu.mx',  '6861230003', @genF,  @carrComputacion, NULL),
 (4 ,'Alumno', N'Luis Ángel Barajas Peña',     '1900104','luis.barajas.demo@uabc.edu.mx',     '6861230004', @genM,  @carrSistemas,    NULL),
 (5 ,'Alumno', N'Regina Paulina Cázares Lugo', '1900105','regina.cazares.demo@uabc.edu.mx',   '6861230005', @genF,  @carrComputacion, N'Migrante'),
 (6 ,'Alumno', N'Emiliano Zúñiga Tapia',       '1900106','emiliano.zuniga.demo@uabc.edu.mx',  '6861230006', @genM,  @carrComputacion, NULL),
 (7 ,'Alumno', N'Fernanda Isabel Quiroz Mata', '1900107','fernanda.quiroz.demo@uabc.edu.mx',  '6861230007', @genF,  @carrComputacion, N'pueblo originario cucapá'),
 (8 ,'Alumno', N'Ricardo Neftalí Ávalos Cota', '1900108','ricardo.avalos.demo@uabc.edu.mx',   '6861230008', @genM,  @carrIndustrial,  NULL),
 (9 ,'Alumno', N'Melissa Guadalupe Torres Sáenz','1900109','melissa.torres.demo@uabc.edu.mx', '6861230009', @genF,  @carrSistemas,    NULL),
 (10,'Alumno', N'Iker Santiago Beltrán Ríos',  '1900110','iker.beltran.demo@uabc.edu.mx',     '6861230010', @genM,  @carrSistemas,    NULL),
 (11,'Alumno', N'Valeria Montserrat Salas Íñiguez','1900111','valeria.salas.demo@uabc.edu.mx','6861230011', @genF,  @carrSistemas,    NULL),
 (12,'Alumno', N'Alexis Yael Robledo Fierro',  '1900112','alexis.robledo.demo@uabc.edu.mx',   '6861230012', @genNB, @carrSistemas,    N'migrante'),
 (13,'Alumno', N'Paola Andrea Nájera Escobedo','1900113','paola.najera.demo@uabc.edu.mx',     '6861230013', @genF,  @carrIndustrial,  NULL),
 (14,'Alumno', N'Héctor Manuel Padilla Grijalva','1900114','hector.padilla.demo@uabc.edu.mx', '6861230014', @genM,  @carrIndustrial,  NULL),
 (15,'Alumno', N'Ximena Alejandra Corral Bañuelos','1900115','ximena.corral.demo@uabc.edu.mx','6861230015', @genF,  @carrIndustrial,  NULL),
 (16,'Alumno', N'Bruno Esteban Villaseñor Loya','1900116','bruno.villasenor.demo@uabc.edu.mx','6861230016', @genNE, @carrMecatronica, NULL),
 (17,'Maestro', N'Mtra. Leticia Aguirre Camacho','19051','leticia.aguirre.demo@uabc.edu.mx',  '6861230051', @genF,  @carrComputacion, NULL),
 (18,'Maestro', N'Dr. Salvador Ochoa Mendívil',  '19052','salvador.ochoa.demo@uabc.edu.mx',   '6861230052', @genM,  @carrSistemas,    NULL);

DECLARE @regMap TABLE (Rn int, Id int);

MERGE Registrantes AS destino
USING (SELECT Rn, Tipo, Nombre, Ident, Correo, Tel, GenId, CarrId, Autodesc FROM @reg) AS origen
    ON destino.Identificador = origen.Ident
WHEN NOT MATCHED THEN
    INSERT (Tipo, NombreCompleto, Identificador, CorreoInstitucional, Telefono,
            CatalogoGeneroId, CatalogoCarreraId, AutodescripcionCultural, Activo, CreadoEn,
            PasswordHash, ProveedorAuth)
    VALUES (origen.Tipo, origen.Nombre, origen.Ident, origen.Correo, origen.Tel,
            origen.GenId, origen.CarrId, origen.Autodesc, 1, @hoy, NULL, 'Google')
OUTPUT origen.Rn, inserted.Id INTO @regMap (Rn, Id);

-- Si la fila ya existia (re-ejecucion), completa el mapa
INSERT INTO @regMap (Rn, Id)
SELECT r.Rn, x.Id FROM @reg r
JOIN Registrantes x ON x.Identificador = r.Ident
WHERE NOT EXISTS (SELECT 1 FROM @regMap m WHERE m.Rn = r.Rn);

-- -------------------------------------------------------------------------------------
-- 2. Equipos + Ficha de Registro (difunto)
-- -------------------------------------------------------------------------------------
DECLARE @eq TABLE (
    En int, Nombre nvarchar(200), Altar nvarchar(200), CarrId int, OrgRn int, MtroRn int,
    Ubicacion nvarchar(200), Catrina bit, Difunto nvarchar(200), FechaDef date,
    Semblanza nvarchar(400), TipoAltar nvarchar(20), Niveles int);

INSERT INTO @eq VALUES
 (1, N'Los Cempasúchil',  N'La Mano Peluda',        @carrComputacion, 1, 17, N'Explanada de Ingeniería, módulo A', 1,
     N'María del Refugio Rocha Sandoval', '2019-11-02', N'Le gustaba tejer, el café de olla y las telenovelas de la tarde.', 'Tradicional', 7),
 (2, N'Xolos del Mictlán', N'El Último Viaje',      @carrComputacion, 5, 17, N'Explanada de Ingeniería, módulo B', 0,
     N'José Antonio Vega Contreras', '2021-03-15', N'Mecánico de toda la vida, fanático de los Xolos y del mariachi.', 'Tradicional', 7),
 (3, N'Flor de Muerto',    N'Camino de Pétalos',    @carrSistemas,    4, 18, N'Pasillo central, edificio de Sistemas', 1,
     N'Guadalupe Esther Lugo Ibarra', '2018-10-28', N'Maestra de primaria durante 32 años; adoraba los girasoles.', 'Tradicional', 3),
 (4, N'Corazón de Copal',  N'Ofrenda para Mateo',   @carrIndustrial,  8, 18, N'Jardín poniente, frente a laboratorios', 0,
     N'Mateo Ávalos Cota', '2020-07-09', N'Niño de 7 años; le encantaban los dinosaurios y los carritos rojos.', 'Ninos', 3);

DECLARE @eqMap TABLE (En int, Id int);

MERGE EquiposConcurso AS destino
USING (SELECT e.En, e.Nombre, e.Altar, e.CarrId, o.Id AS OrgId, m.Id AS MtroId, e.Ubicacion, e.Catrina
       FROM @eq e
       JOIN @regMap o ON o.Rn = e.OrgRn
       JOIN @regMap m ON m.Rn = e.MtroRn) AS origen
    ON destino.Nombre = origen.Nombre AND destino.Periodo = @periodo
WHEN NOT MATCHED THEN
    INSERT (Nombre, NombreAltar, CarreraId, Periodo, CreadoPorRegistranteId, MaestroEncargadoId,
            UbicacionAltar, Activo, CreadoEn, MaestroEncargadoIdentificadorPendiente, HaceCatrina)
    VALUES (origen.Nombre, origen.Altar, origen.CarrId, @periodo, origen.OrgId, origen.MtroId,
            origen.Ubicacion, 1, @hoy, NULL, origen.Catrina)
OUTPUT origen.En, inserted.Id INTO @eqMap (En, Id);

INSERT INTO @eqMap (En, Id)
SELECT e.En, x.Id FROM @eq e
JOIN EquiposConcurso x ON x.Nombre = e.Nombre AND x.Periodo = @periodo
WHERE NOT EXISTS (SELECT 1 FROM @eqMap m WHERE m.En = e.En);

INSERT INTO Difuntos (EquipoId, Nombre, FechaDefuncion, SemblanzaHobbiesTematica, TipoAltar)
SELECT m.Id, e.Difunto, e.FechaDef, e.Semblanza, e.TipoAltar
FROM @eq e JOIN @eqMap m ON m.En = e.En
WHERE NOT EXISTS (SELECT 1 FROM Difuntos d WHERE d.EquipoId = m.Id);

-- -------------------------------------------------------------------------------------
-- 3. Integrantes (un Narrador por equipo)
-- -------------------------------------------------------------------------------------
DECLARE @mem TABLE (En int, Rn int, Rol nvarchar(20));
INSERT INTO @mem VALUES
 (1,1,'Integrante'), (1,2,'Narrador'), (1,3,'Integrante'), (1,7,'Integrante'),
 (2,5,'Integrante'), (2,6,'Narrador'),
 (3,4,'Integrante'), (3,9,'Narrador'), (3,10,'Integrante'), (3,11,'Integrante'), (3,12,'Integrante'),
 (4,8,'Integrante'), (4,13,'Narrador'), (4,14,'Integrante'), (4,15,'Integrante');

INSERT INTO RegistranteEquipos (RegistranteId, EquipoId, Rol, FechaIngreso, Periodo)
SELECT r.Id, q.Id, m.Rol, @hoy, @periodo
FROM @mem m
JOIN @regMap r ON r.Rn = m.Rn
JOIN @eqMap q ON q.En = m.En
WHERE NOT EXISTS (SELECT 1 FROM RegistranteEquipos re WHERE re.RegistranteId = r.Id AND re.Periodo = @periodo);

-- -------------------------------------------------------------------------------------
-- 4. Evaluaciones (equipos 1 y 2 en Final, equipo 3 en Preliminar; equipo 4 sin evaluar)
-- -------------------------------------------------------------------------------------
DECLARE @ev TABLE (En int, Estado nvarchar(20), DN decimal(5,2), NAR decimal(5,2),
                   TEM decimal(5,2), EP decimal(5,2), VG decimal(5,2));
INSERT INTO @ev VALUES
 (1, 'Final',      9.0, 9.5, 9.2, 9.0, 9.4),
 (2, 'Final',      8.0, 7.5, 8.2, 8.0, 8.3),
 (3, 'Preliminar', 8.5, 9.0, 8.8, 8.7, 8.9);

DECLARE @evMap TABLE (En int, Id int);

MERGE EvaluacionesConcurso AS destino
USING (SELECT v.En, q.Id AS EquipoId, e.Niveles, v.Estado, v.DN, v.NAR, v.TEM, v.EP, v.VG,
              e.Nombre, e.Altar, e.Difunto, e.FechaDef, e.Catrina
       FROM @ev v
       JOIN @eq e ON e.En = v.En
       JOIN @eqMap q ON q.En = v.En) AS origen
    ON destino.EquipoId = origen.EquipoId
WHEN NOT MATCHED THEN
    INSERT (EquipoId, JuezId, Periodo, Niveles, Estado, PuntajeElementos, DistribucionNiveles,
            Narrador, TematicaHobbies, ObjetivoCultural, EsenciaPersonalidad, ValoracionGeneral,
            IncluyeCatrina, NotaFinal, Lugar, SnapshotNombreEquipo, SnapshotNombreAltar,
            SnapshotDifuntoNombre, SnapshotDifuntoFechaDefuncion, CreadoEn, ActualizadoEn,
            EsenciaPersonalidadFinal)
    VALUES (origen.EquipoId, @juezId, @periodo, origen.Niveles, origen.Estado, 0, origen.DN,
            origen.NAR, origen.TEM, 0, origen.EP, origen.VG,
            origen.Catrina, NULL, NULL, origen.Nombre, origen.Altar,
            origen.Difunto, origen.FechaDef, @hoy, @hoy, NULL)
OUTPUT origen.En, inserted.Id INTO @evMap (En, Id);

INSERT INTO @evMap (En, Id)
SELECT v.En, x.Id FROM @ev v
JOIN @eqMap q ON q.En = v.En
JOIN EvaluacionesConcurso x ON x.EquipoId = q.Id
WHERE NOT EXISTS (SELECT 1 FROM @evMap m WHERE m.En = v.En);

-- Integrantes congelados en la evaluacion (snapshot)
INSERT INTO EvaluacionIntegrantes (EvaluacionId, RegistranteId, NombreCompleto, Identificador, Rol)
SELECT ev.Id, r.Id, g.Nombre, g.Ident, m.Rol
FROM @mem m
JOIN @evMap ev ON ev.En = m.En
JOIN @regMap r ON r.Rn = m.Rn
JOIN @reg g ON g.Rn = m.Rn
WHERE NOT EXISTS (SELECT 1 FROM EvaluacionIntegrantes ei WHERE ei.EvaluacionId = ev.Id AND ei.RegistranteId = r.Id);

-- -------------------------------------------------------------------------------------
-- 5. Checklist de los 24 elementos por evaluacion
--     Equipo 1: altar muy completo | Equipo 2: le faltan varios | Equipo 3: intermedio
-- -------------------------------------------------------------------------------------
INSERT INTO ElementosEvaluados (EvaluacionId, ElementoId, Satisfaccion, NivelAsignado, Tematizado)
SELECT ev.Id,
       el.Id,
       CASE
         WHEN ev.En = 1 THEN CASE WHEN el.Orden % 7 = 0 THEN 'Regular' ELSE 'Bueno' END
         WHEN ev.En = 2 THEN CASE WHEN el.Orden % 5 = 0 THEN 'NoLoTiene'
                                  WHEN el.Orden % 3 = 0 THEN 'Regular'
                                  ELSE 'Bueno' END
         ELSE            CASE WHEN el.Orden % 6 = 0 THEN 'Regular' ELSE 'Bueno' END
       END,
       CASE WHEN ec.Niveles = 3 THEN el.NivelSugerido3 ELSE el.NivelSugerido7 END,
       CASE WHEN ev.En = 1 AND el.Orden % 4 = 0 THEN 1
            WHEN ev.En = 3 AND el.Orden % 8 = 0 THEN 1
            ELSE 0 END
FROM @evMap ev
JOIN EvaluacionesConcurso ec ON ec.Id = ev.Id
CROSS JOIN Elementos el
WHERE el.Activo = 1
  AND NOT EXISTS (SELECT 1 FROM ElementosEvaluados x WHERE x.EvaluacionId = ev.Id AND x.ElementoId = el.Id);

-- -------------------------------------------------------------------------------------
-- 6. Catrina (equipos 1 y 3 la declararon; solo la del 1 esta evaluada en Final)
-- -------------------------------------------------------------------------------------
INSERT INTO EvaluacionesCatrina (EvaluacionId, Vestuario, Maquillaje, Accesorios,
                                 OriginalidadCreatividad, ApegoTradiciones, NotaCatrina, LugarCatrina)
SELECT ev.Id, 9.5, 9.0, 8.5, 9.5, 9.0, NULL, NULL
FROM @evMap ev
WHERE ev.En = 1
  AND NOT EXISTS (SELECT 1 FROM EvaluacionesCatrina c WHERE c.EvaluacionId = ev.Id);

-- -------------------------------------------------------------------------------------
-- 7. Calculo de notas con las formulas del sistema (vision.md 7.2 y 7.7)
-- -------------------------------------------------------------------------------------
-- PuntajeElementos = (suma(valor*peso) / suma(peso_max)) * 10   y  ObjetivoCultural = PuntajeElementos
UPDATE e
SET PuntajeElementos = calc.Puntaje,
    ObjetivoCultural = calc.Puntaje
FROM EvaluacionesConcurso e
JOIN @evMap m ON m.Id = e.Id
CROSS APPLY (
    SELECT CAST(ROUND(
        SUM(CASE ee.Satisfaccion
              WHEN 'NoLoTiene' THEN @cfgNoLoTiene
              WHEN 'Regular' THEN @cfgRegular
              ELSE @cfgBueno END
            * CASE el.Categoria WHEN 'RitualObligatorio' THEN @cfgRitual ELSE @cfgDeco END)
        / NULLIF(SUM(CASE el.Categoria WHEN 'RitualObligatorio' THEN @cfgRitual ELSE @cfgDeco END * @cfgBueno), 0)
        * 10, 2) AS decimal(18,2)) AS Puntaje
    FROM ElementosEvaluados ee
    JOIN Elementos el ON el.Id = ee.ElementoId
    WHERE ee.EvaluacionId = e.Id
) calc;

-- EsenciaPersonalidadFinal = min(10, EsenciaPersonalidad + tematizados * bonus)
UPDATE e
SET EsenciaPersonalidadFinal = CASE WHEN e.EsenciaPersonalidad + t.Bonus > 10 THEN 10
                                    ELSE CAST(ROUND(e.EsenciaPersonalidad + t.Bonus, 2) AS decimal(18,2)) END
FROM EvaluacionesConcurso e
JOIN @evMap m ON m.Id = e.Id
CROSS APPLY (SELECT COUNT(*) * @cfgBonus AS Bonus FROM ElementosEvaluados ee
             WHERE ee.EvaluacionId = e.Id AND ee.Tematizado = 1) t;

-- NotaFinal solo para las evaluaciones en Final
UPDATE e
SET NotaFinal = CAST(ROUND(
        e.ObjetivoCultural * @cfgPesoOC
      + ISNULL(e.EsenciaPersonalidadFinal, e.EsenciaPersonalidad) * @cfgPesoEP
      + e.ValoracionGeneral * @cfgPesoVG
      + e.DistribucionNiveles * @cfgPesoDN
      + e.Narrador * @cfgPesoNAR, 2) AS decimal(18,2))
FROM EvaluacionesConcurso e
JOIN @evMap m ON m.Id = e.Id
WHERE e.Estado = 'Final';

UPDATE c
SET NotaCatrina = CAST(ROUND(c.Vestuario * @cfgCatVest + c.Maquillaje * @cfgCatMaq
                           + c.Accesorios * @cfgCatAcc + c.OriginalidadCreatividad * @cfgCatOrig
                           + c.ApegoTradiciones * @cfgCatTrad, 2) AS decimal(18,2))
FROM EvaluacionesCatrina c
JOIN EvaluacionesConcurso e ON e.Id = c.EvaluacionId
JOIN @evMap m ON m.Id = e.Id;

-- Lugar 1/2/3 por carrera del equipo y periodo, solo entre las Final
WITH ranking AS (
    SELECT e.Id,
           ROW_NUMBER() OVER (PARTITION BY q.CarreraId ORDER BY e.NotaFinal DESC) AS Pos
    FROM EvaluacionesConcurso e
    JOIN EquiposConcurso q ON q.Id = e.EquipoId
    WHERE e.Estado = 'Final' AND e.Periodo = @periodo
)
UPDATE e SET Lugar = CASE WHEN r.Pos <= 3 THEN r.Pos ELSE NULL END
FROM EvaluacionesConcurso e JOIN ranking r ON r.Id = e.Id;

WITH rankingCat AS (
    SELECT c.Id,
           ROW_NUMBER() OVER (PARTITION BY q.CarreraId ORDER BY c.NotaCatrina DESC) AS Pos
    FROM EvaluacionesCatrina c
    JOIN EvaluacionesConcurso e ON e.Id = c.EvaluacionId
    JOIN EquiposConcurso q ON q.Id = e.EquipoId
    WHERE e.Estado = 'Final' AND e.Periodo = @periodo AND c.NotaCatrina IS NOT NULL
)
UPDATE c SET LugarCatrina = CASE WHEN r.Pos <= 3 THEN r.Pos ELSE NULL END
FROM EvaluacionesCatrina c JOIN rankingCat r ON r.Id = c.Id;

COMMIT TRANSACTION;

-- -------------------------------------------------------------------------------------
-- Resumen
-- -------------------------------------------------------------------------------------
SELECT 'Registrantes demo' AS Concepto, COUNT(*) AS Total FROM Registrantes WHERE CorreoInstitucional LIKE '%.demo@uabc.edu.mx'
UNION ALL SELECT 'Equipos', COUNT(*) FROM EquiposConcurso WHERE Periodo = @periodo
UNION ALL SELECT 'Evaluaciones', COUNT(*) FROM EvaluacionesConcurso WHERE Periodo = @periodo;

SELECT q.Nombre AS Equipo, c.Nombre AS Carrera, e.Estado, e.PuntajeElementos,
       e.ObjetivoCultural, e.EsenciaPersonalidadFinal, e.NotaFinal, e.Lugar,
       cat.NotaCatrina, cat.LugarCatrina
FROM EvaluacionesConcurso e
JOIN EquiposConcurso q ON q.Id = e.EquipoId
JOIN CatalogoCarreras c ON c.Id = q.CarreraId
LEFT JOIN EvaluacionesCatrina cat ON cat.EvaluacionId = e.Id
WHERE e.Periodo = @periodo
ORDER BY c.Nombre, e.NotaFinal DESC;
