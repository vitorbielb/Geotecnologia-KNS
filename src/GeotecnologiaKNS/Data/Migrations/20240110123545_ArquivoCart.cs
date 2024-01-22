using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class ArquivoCart : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CartografiaId",
                table: "Solicitacao",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitacao_CartografiaId",
                table: "Solicitacao",
                column: "CartografiaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitacao_Cartografias_CartografiaId",
                table: "Solicitacao",
                column: "CartografiaId",
                principalTable: "Cartografias",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Solicitacao_Cartografias_CartografiaId",
                table: "Solicitacao");

            migrationBuilder.DropIndex(
                name: "IX_Solicitacao_CartografiaId",
                table: "Solicitacao");

            migrationBuilder.DropColumn(
                name: "CartografiaId",
                table: "Solicitacao");
        }
    }
}
