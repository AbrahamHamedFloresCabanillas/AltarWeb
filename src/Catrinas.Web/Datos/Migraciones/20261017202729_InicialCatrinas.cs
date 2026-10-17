using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catrinas.Web.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class InicialCatrinas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carreras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carreras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesPeriodo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FechaLimiteInscripcion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaLimiteRequisitos = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PesoVestuario = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoMaquillaje = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoAccesorios = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoOriginalidadCreatividad = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoApegoTradiciones = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    UmbralAgrupacionDemografica = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesPeriodo", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionesPeriodo_Pesos", "[PesoVestuario] + [PesoMaquillaje] + [PesoAccesorios] + [PesoOriginalidadCreatividad] + [PesoApegoTradiciones] = 1 AND [PesoVestuario] >= 0 AND [PesoMaquillaje] >= 0 AND [PesoAccesorios] >= 0 AND [PesoOriginalidadCreatividad] >= 0 AND [PesoApegoTradiciones] >= 0");
                    table.CheckConstraint("CK_ConfiguracionesPeriodo_Umbral", "[UmbralAgrupacionDemografica] >= 3");
                });

            migrationBuilder.CreateTable(
                name: "Generos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Generos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jueces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorreoInstitucional = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Identificador = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    ProveedorAuth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Pendiente = table.Column<bool>(type: "bit", nullable: false),
                    DebeCambiarPassword = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaBaja = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jueces", x => x.Id);
                    table.CheckConstraint("CK_Jueces_Rol", "[Rol] IN ('Juez','Admin')");
                });

            migrationBuilder.CreateTable(
                name: "Registrantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identificador = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CorreoInstitucional = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProveedorAuth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CatalogoGeneroId = table.Column<int>(type: "int", nullable: true),
                    CatalogoCarreraId = table.Column<int>(type: "int", nullable: true),
                    AutodescripcionCultural = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrantes", x => x.Id);
                    table.CheckConstraint("CK_Registrantes_Tipo", "[Tipo] IN ('Alumno','Maestro','Administrativo')");
                    table.ForeignKey(
                        name: "FK_Registrantes_Carreras_CatalogoCarreraId",
                        column: x => x.CatalogoCarreraId,
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Registrantes_Generos_CatalogoGeneroId",
                        column: x => x.CatalogoGeneroId,
                        principalTable: "Generos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inscripciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CarreraId = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ResponsableId = table.Column<int>(type: "int", nullable: false),
                    MaestroEncargadoId = table.Column<int>(type: "int", nullable: true),
                    AltarExternalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AltarNombreSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inscripciones", x => x.Id);
                    table.CheckConstraint("CK_Inscripciones_AltarExternalId", "[AltarExternalId] IS NULL OR [AltarExternalId] LIKE 'ALT-[0-9][0-9][0-9][0-9]-[12]-[0-9][0-9][0-9][0-9]%'");
                    table.ForeignKey(
                        name: "FK_Inscripciones_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "Carreras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Registrantes_MaestroEncargadoId",
                        column: x => x.MaestroEncargadoId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Registrantes_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evaluaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InscripcionId = table.Column<int>(type: "int", nullable: false),
                    JuezId = table.Column<int>(type: "int", nullable: true),
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Vestuario = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Maquillaje = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Accesorios = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    OriginalidadCreatividad = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ApegoTradiciones = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PesoVestuario = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoMaquillaje = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoAccesorios = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoOriginalidadCreatividad = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PesoApegoTradiciones = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    NotaCatrina = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Lugar = table.Column<int>(type: "int", nullable: true),
                    SnapshotNombreInscripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SnapshotCarrera = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SnapshotPersonificador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotMaestroEncargado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SnapshotAltarExternalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SnapshotAltarNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizadaEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluaciones", x => x.Id);
                    table.CheckConstraint("CK_Evaluaciones_Criterios", "[Vestuario] BETWEEN 0 AND 10 AND [Maquillaje] BETWEEN 0 AND 10 AND [Accesorios] BETWEEN 0 AND 10 AND [OriginalidadCreatividad] BETWEEN 0 AND 10 AND [ApegoTradiciones] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_Evaluaciones_Estado", "[Estado] IN ('Preliminar','Final')");
                    table.CheckConstraint("CK_Evaluaciones_Final", "[Estado] <> 'Final' OR [FinalizadaEn] IS NOT NULL");
                    table.CheckConstraint("CK_Evaluaciones_Lugar", "[Lugar] IS NULL OR ([Lugar] BETWEEN 1 AND 3 AND [Estado] = 'Final')");
                    table.CheckConstraint("CK_Evaluaciones_Pesos", "[PesoVestuario] + [PesoMaquillaje] + [PesoAccesorios] + [PesoOriginalidadCreatividad] + [PesoApegoTradiciones] = 1");
                    table.ForeignKey(
                        name: "FK_Evaluaciones_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evaluaciones_Jueces_JuezId",
                        column: x => x.JuezId,
                        principalTable: "Jueces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MiembrosInscripcion",
                columns: table => new
                {
                    InscripcionId = table.Column<int>(type: "int", nullable: false),
                    RegistranteId = table.Column<int>(type: "int", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembrosInscripcion", x => new { x.InscripcionId, x.RegistranteId });
                    table.CheckConstraint("CK_MiembrosInscripcion_Rol", "[Rol] IN ('Integrante','Personificador')");
                    table.ForeignKey(
                        name: "FK_MiembrosInscripcion_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MiembrosInscripcion_Registrantes_RegistranteId",
                        column: x => x.RegistranteId,
                        principalTable: "Registrantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrantesEvaluados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluacionId = table.Column<int>(type: "int", nullable: false),
                    RegistranteId = table.Column<int>(type: "int", nullable: true),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Identificador = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    CorreoInstitucional = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrantesEvaluados", x => x.Id);
                    table.CheckConstraint("CK_IntegrantesEvaluados_Rol", "[Rol] IN ('Integrante','Personificador')");
                    table.ForeignKey(
                        name: "FK_IntegrantesEvaluados_Evaluaciones_EvaluacionId",
                        column: x => x.EvaluacionId,
                        principalTable: "Evaluaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Carreras",
                columns: new[] { "Id", "Activo", "Nombre", "Orden" },
                values: new object[,]
                {
                    { 1, true, "Lic. en Sistemas Computacionales", 1 },
                    { 2, true, "Bioingeniero", 2 },
                    { 3, true, "Ing. Aeroespacial", 3 },
                    { 4, true, "Ing. Civil", 4 },
                    { 5, true, "Ing. en Computación", 5 },
                    { 6, true, "Ing. en Electrónica", 6 },
                    { 7, true, "Ing. Eléctrico", 7 },
                    { 8, true, "Ing. en Energías Renovables", 8 },
                    { 9, true, "Ing. Industrial", 9 },
                    { 10, true, "Ing. Mecánico", 10 },
                    { 11, true, "Ing. en Mecatrónica", 11 },
                    { 12, true, "Ing. en Semiconductores y Microelectrónica", 12 },
                    { 13, true, "Ing. de Datos e Inteligencia Artificial", 13 }
                });

            migrationBuilder.InsertData(
                table: "Generos",
                columns: new[] { "Id", "Activo", "Nombre", "Orden" },
                values: new object[,]
                {
                    { 1, true, "Masculino", 1 },
                    { 2, true, "Femenino", 2 },
                    { 3, true, "No binario", 3 },
                    { 4, true, "Prefiero no especificar", 4 },
                    { 5, true, "Otro", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carreras_Nombre",
                table: "Carreras",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPeriodo_Periodo",
                table: "ConfiguracionesPeriodo",
                column: "Periodo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_InscripcionId",
                table: "Evaluaciones",
                column: "InscripcionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_JuezId",
                table: "Evaluaciones",
                column: "JuezId");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluaciones_Periodo_Estado",
                table: "Evaluaciones",
                columns: new[] { "Periodo", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Generos_Nombre",
                table: "Generos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_CarreraId",
                table: "Inscripciones",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_MaestroEncargadoId",
                table: "Inscripciones",
                column: "MaestroEncargadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_Nombre_Periodo",
                table: "Inscripciones",
                columns: new[] { "Nombre", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_ResponsableId",
                table: "Inscripciones",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrantesEvaluados_EvaluacionId_RegistranteId",
                table: "IntegrantesEvaluados",
                columns: new[] { "EvaluacionId", "RegistranteId" },
                unique: true,
                filter: "[RegistranteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Jueces_CorreoInstitucional",
                table: "Jueces",
                column: "CorreoInstitucional",
                unique: true,
                filter: "[CorreoInstitucional] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Jueces_Usuario",
                table: "Jueces",
                column: "Usuario",
                unique: true,
                filter: "[Usuario] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosInscripcion_RegistranteId_Periodo",
                table: "MiembrosInscripcion",
                columns: new[] { "RegistranteId", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosInscripcion_UnPersonificador",
                table: "MiembrosInscripcion",
                column: "InscripcionId",
                unique: true,
                filter: "[Rol] = 'Personificador'");

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_CatalogoCarreraId",
                table: "Registrantes",
                column: "CatalogoCarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_CatalogoGeneroId",
                table: "Registrantes",
                column: "CatalogoGeneroId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_CorreoInstitucional",
                table: "Registrantes",
                column: "CorreoInstitucional",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrantes_Identificador",
                table: "Registrantes",
                column: "Identificador",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesPeriodo");

            migrationBuilder.DropTable(
                name: "IntegrantesEvaluados");

            migrationBuilder.DropTable(
                name: "MiembrosInscripcion");

            migrationBuilder.DropTable(
                name: "Evaluaciones");

            migrationBuilder.DropTable(
                name: "Inscripciones");

            migrationBuilder.DropTable(
                name: "Jueces");

            migrationBuilder.DropTable(
                name: "Registrantes");

            migrationBuilder.DropTable(
                name: "Carreras");

            migrationBuilder.DropTable(
                name: "Generos");
        }
    }
}
