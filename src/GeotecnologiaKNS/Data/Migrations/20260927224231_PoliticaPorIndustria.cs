using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class PoliticaPorIndustria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PoliticaAplicada",
                table: "AnalisesAutomaticas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Politicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AtualizadaEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Politicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Politicas_Industrias_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Industrias",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Regras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoliticaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Severidade = table.Column<int>(type: "int", nullable: false),
                    AreaMinimaHa = table.Column<double>(type: "float", nullable: false),
                    PercentualMinimo = table.Column<double>(type: "float", nullable: false),
                    AnoMinimo = table.Column<int>(type: "int", nullable: true),
                    Fundamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ativa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regras_Politicas_PoliticaId",
                        column: x => x.PoliticaId,
                        principalTable: "Politicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Politicas_TenantId",
                table: "Politicas",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regras_PoliticaId",
                table: "Regras",
                column: "PoliticaId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Regras");

            migrationBuilder.DropTable(
                name: "Politicas");

            migrationBuilder.DropColumn(
                name: "PoliticaAplicada",
                table: "AnalisesAutomaticas");
        }
    }
}
