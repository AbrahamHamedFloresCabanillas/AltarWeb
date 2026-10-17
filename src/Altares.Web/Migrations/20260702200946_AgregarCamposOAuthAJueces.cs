using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposOAuthAJueces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Usuario",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "CorreoInstitucional",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identificador",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Pendiente",
                table: "Jueces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProveedorAuth",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Local");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorreoInstitucional",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "Identificador",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "Pendiente",
                table: "Jueces");

            migrationBuilder.DropColumn(
                name: "ProveedorAuth",
                table: "Jueces");

            migrationBuilder.AlterColumn<string>(
                name: "Usuario",
                table: "Jueces",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
