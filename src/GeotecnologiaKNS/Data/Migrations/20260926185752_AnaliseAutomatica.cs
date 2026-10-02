using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class AnaliseAutomatica : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalisesAutomaticas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SolicitacaoId = table.Column<int>(type: "int", nullable: false),
                    CodigoCar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Politica = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaImovelHa = table.Column<double>(type: "float", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<int>(type: "int", nullable: false),
                    Parecer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CamadasVerificadas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Erro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IniciadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcluidaEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisesAutomaticas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalisesAutomaticas_Industrias_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Industrias",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalisesAutomaticas_Solicitacao_SolicitacaoId",
                        column: x => x.SolicitacaoId,
                        principalTable: "Solicitacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalisesOcorrencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnaliseId = table.Column<int>(type: "int", nullable: false),
                    CodigoRegra = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Severidade = table.Column<int>(type: "int", nullable: false),
                    Camada = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rotulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AreaSobrepostaHa = table.Column<double>(type: "float", nullable: false),
                    PercentualDoImovel = table.Column<double>(type: "float", nullable: false),
                    Fundamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalisesOcorrencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalisesOcorrencias_AnalisesAutomaticas_AnaliseId",
                        column: x => x.AnaliseId,
                        principalTable: "AnalisesAutomaticas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalisesAutomaticas_SolicitacaoId",
                table: "AnalisesAutomaticas",
                column: "SolicitacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalisesAutomaticas_TenantId",
                table: "AnalisesAutomaticas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalisesOcorrencias_AnaliseId",
                table: "AnalisesOcorrencias",
                column: "AnaliseId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalisesOcorrencias");

            migrationBuilder.DropTable(
                name: "AnalisesAutomaticas");
        }
    }
}
