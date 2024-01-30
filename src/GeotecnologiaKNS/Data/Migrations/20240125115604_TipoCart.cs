using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class TipoCart : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Cartografias");

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "CartografiasArquivos",
                type: "int",
                nullable: false,
                defaultValue: 0);

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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cartografias_CartografiasArquivos_CartografiaArquivoId",
                table: "Cartografias");

            migrationBuilder.DropIndex(
                name: "IX_Cartografias_CartografiaArquivoId",
                table: "Cartografias");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "CartografiasArquivos");

            migrationBuilder.DropColumn(
                name: "CartografiaArquivoId",
                table: "Cartografias");

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Cartografias",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
