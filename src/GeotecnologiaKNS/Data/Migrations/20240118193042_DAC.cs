using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class DAC : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cartografias_CartografiasArquivos_CartografiaArquivoId",
                table: "Cartografias");

            migrationBuilder.DropIndex(
                name: "IX_Cartografias_CartografiaArquivoId",
                table: "Cartografias");

            migrationBuilder.DropColumn(
                name: "CartografiaArquivoId",
                table: "Cartografias");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CartografiaArquivoId",
                table: "Cartografias",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_CartografiaArquivoId",
                table: "Cartografias",
                column: "CartografiaArquivoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cartografias_CartografiasArquivos_CartografiaArquivoId",
                table: "Cartografias",
                column: "CartografiaArquivoId",
                principalTable: "CartografiasArquivos",
                principalColumn: "Id");
        }
    }
}
