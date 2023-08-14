using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class AddPropriedadeProdutor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProdutorId",
                table: "Propriedades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Propriedades_ProdutorId",
                table: "Propriedades",
                column: "ProdutorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Propriedades_Produtores_ProdutorId",
                table: "Propriedades",
                column: "ProdutorId",
                principalTable: "Produtores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propriedades_Produtores_ProdutorId",
                table: "Propriedades");

            migrationBuilder.DropIndex(
                name: "IX_Propriedades_ProdutorId",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "ProdutorId",
                table: "Propriedades");
        }
    }
}
