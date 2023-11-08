using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class Atualizado : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Validacao",
                table: "Propriedades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Situacao",
                table: "Produtores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SolicitacoesId",
                table: "Produtores",
                type: "int",
                nullable: true);

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
                name: "IX_Produtores_SolicitacoesId",
                table: "Produtores",
                column: "SolicitacoesId");

            migrationBuilder.CreateIndex(
                name: "IX_Validade_ProdutorId",
                table: "Validade",
                column: "ProdutorId");

            migrationBuilder.CreateIndex(
                name: "IX_validadePropriedades_PropriedadeId",
                table: "validadePropriedades",
                column: "PropriedadeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Produtores_Solicitacao_SolicitacoesId",
                table: "Produtores",
                column: "SolicitacoesId",
                principalTable: "Solicitacao",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Produtores_Solicitacao_SolicitacoesId",
                table: "Produtores");

            migrationBuilder.DropTable(
                name: "Validade");

            migrationBuilder.DropTable(
                name: "validadePropriedades");

            migrationBuilder.DropIndex(
                name: "IX_Produtores_SolicitacoesId",
                table: "Produtores");

            migrationBuilder.DropColumn(
                name: "Validacao",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Situacao",
                table: "Produtores");

            migrationBuilder.DropColumn(
                name: "SolicitacoesId",
                table: "Produtores");
        }
    }
}
