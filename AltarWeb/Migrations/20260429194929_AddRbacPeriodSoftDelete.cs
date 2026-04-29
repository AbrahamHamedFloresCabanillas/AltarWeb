using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacPeriodSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evaluaciones_Jueces_JuezId",
                table: "Evaluaciones");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminado",
                table: "Jueces",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Jueces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NombreCompleto",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rol",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "JuezId",
                table: "Evaluaciones",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "NombreJuez",
                table: "Evaluaciones",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Periodo",
                table: "Evaluaciones",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Evaluaciones_Jueces_JuezId",
                table: "Evaluaciones",
                column: "JuezId",
                principalTable: "Jueces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evaluaciones_Jueces_JuezId",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "FechaEliminado",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "NombreCompleto",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "Rol",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "NombreJuez",
                table: "Evaluaciones");

            migrationBuilder.DropColumn(
                name: "Periodo",
                table: "Evaluaciones");

            migrationBuilder.AlterColumn<int>(
                name: "JuezId",
                table: "Evaluaciones",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Evaluaciones_Jueces_JuezId",
                table: "Evaluaciones",
                column: "JuezId",
                principalTable: "Jueces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
