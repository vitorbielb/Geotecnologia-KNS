using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class OtimizacaoDoCodigo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analistas");

            migrationBuilder.DropTable(
                name: "Validade");

            migrationBuilder.DropTable(
                name: "validadePropriedades");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analistas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitacaoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analistas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Analistas_Solicitacao_SolicitacaoId",
                        column: x => x.SolicitacaoId,
                        principalTable: "Solicitacao",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Validade",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProdutorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validade", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Validade_Produtores_ProdutorId",
                        column: x => x.ProdutorId,
                        principalTable: "Produtores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "validadePropriedades",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropriedadeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validadePropriedades", x => x.ID);
                    table.ForeignKey(
                        name: "FK_validadePropriedades_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analistas_SolicitacaoId",
                table: "Analistas",
                column: "SolicitacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Validade_ProdutorId",
                table: "Validade",
                column: "ProdutorId");

            migrationBuilder.CreateIndex(
                name: "IX_validadePropriedades_PropriedadeId",
                table: "validadePropriedades",
                column: "PropriedadeId");
        }
    }
}
