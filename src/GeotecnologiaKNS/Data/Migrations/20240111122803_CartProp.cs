using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class CartProp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cartografias_PropriedadeId",
                table: "Cartografias");

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_PropriedadeId",
                table: "Cartografias",
                column: "PropriedadeId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cartografias_PropriedadeId",
                table: "Cartografias");

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_PropriedadeId",
                table: "Cartografias",
                column: "PropriedadeId");
        }
    }
}
