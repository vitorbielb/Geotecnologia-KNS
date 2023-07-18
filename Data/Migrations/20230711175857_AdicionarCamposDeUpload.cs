using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposDeUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CadastroAmbientalRural",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ccir",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Incra",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LicencaAmbiental",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Matricula",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Outros",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CadastroAmbientalRural",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Ccir",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Incra",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "LicencaAmbiental",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Matricula",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Outros",
                table: "Propriedades");
        }
    }
}
