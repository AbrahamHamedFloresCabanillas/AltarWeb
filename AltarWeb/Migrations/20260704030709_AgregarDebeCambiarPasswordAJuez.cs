using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDebeCambiarPasswordAJuez : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarPassword",
                table: "Jueces",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebeCambiarPassword",
                table: "Jueces");
        }
    }
}
