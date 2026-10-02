using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class CoberturaDaAnalise : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O padrão false é proposital, e não um resto do scaffold: as análises
            // já gravadas rodaram quando só existia uma camada, então a cobertura
            // delas era mesmo parcial. Marcá-las como completas seria afirmar uma
            // verificação que não houve — e os laudos antigos passam a exibir o
            // aviso, corretamente.
            migrationBuilder.AddColumn<bool>(
                name: "CoberturaCompleta",
                table: "AnalisesAutomaticas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RegrasNaoAvaliadas",
                table: "AnalisesAutomaticas",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoberturaCompleta",
                table: "AnalisesAutomaticas");

            migrationBuilder.DropColumn(
                name: "RegrasNaoAvaliadas",
                table: "AnalisesAutomaticas");
        }
    }
}
