using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class nsei : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_TenantId",
                table: "Cartografias",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cartografias_Industrias_TenantId",
                table: "Cartografias",
                column: "TenantId",
                principalTable: "Industrias",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cartografias_Industrias_TenantId",
                table: "Cartografias");

            migrationBuilder.DropIndex(
                name: "IX_Cartografias_TenantId",
                table: "Cartografias");
        }
    }
}
