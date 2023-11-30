using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class AnaliseArquivosDb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnaliseArquivo_Solicitacao_SolicitacaoId",
                table: "AnaliseArquivo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AnaliseArquivo",
                table: "AnaliseArquivo");

            migrationBuilder.RenameTable(
                name: "AnaliseArquivo",
                newName: "AnalisesArquivos");

            migrationBuilder.RenameIndex(
                name: "IX_AnaliseArquivo_SolicitacaoId",
                table: "AnalisesArquivos",
                newName: "IX_AnalisesArquivos_SolicitacaoId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AnalisesArquivos",
                table: "AnalisesArquivos",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AnalisesArquivos_Solicitacao_SolicitacaoId",
                table: "AnalisesArquivos",
                column: "SolicitacaoId",
                principalTable: "Solicitacao",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalisesArquivos_Solicitacao_SolicitacaoId",
                table: "AnalisesArquivos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AnalisesArquivos",
                table: "AnalisesArquivos");

            migrationBuilder.RenameTable(
                name: "AnalisesArquivos",
                newName: "AnaliseArquivo");

            migrationBuilder.RenameIndex(
                name: "IX_AnalisesArquivos_SolicitacaoId",
                table: "AnaliseArquivo",
                newName: "IX_AnaliseArquivo_SolicitacaoId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AnaliseArquivo",
                table: "AnaliseArquivo",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AnaliseArquivo_Solicitacao_SolicitacaoId",
                table: "AnaliseArquivo",
                column: "SolicitacaoId",
                principalTable: "Solicitacao",
                principalColumn: "Id");
        }
    }
}
