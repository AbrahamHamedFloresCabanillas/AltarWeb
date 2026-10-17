using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltarWeb.Migrations
{
    /// <inheritdoc />
    public partial class SeedNivelSugeridoElementos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NivelSugerido",
                table: "Elementos",
                newName: "NivelSugerido7");

            migrationBuilder.AddColumn<int>(
                name: "NivelSugerido3",
                table: "Elementos",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 2 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 2 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 2 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 2, 4 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 3, 6 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 2, 3 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 3, 7 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 2, 3 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 2 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 2 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 3, 6 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 2, 3 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 1 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 2, 3 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 2, 4 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 1 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 3, 7 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 3, 7 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 3, 7 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 1 });

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "NivelSugerido3", "NivelSugerido7" },
                values: new object[] { 1, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NivelSugerido3",
                table: "Elementos");

            migrationBuilder.RenameColumn(
                name: "NivelSugerido7",
                table: "Elementos",
                newName: "NivelSugerido");

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 1,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 2,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 3,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 4,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 5,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 6,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 7,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 8,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 9,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 10,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 11,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 12,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 13,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 14,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 15,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 16,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 17,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 18,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 19,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 20,
                column: "NivelSugerido",
                value: null);

            migrationBuilder.UpdateData(
                table: "Elementos",
                keyColumn: "Id",
                keyValue: 21,
                column: "NivelSugerido",
                value: null);
        }
    }
}
