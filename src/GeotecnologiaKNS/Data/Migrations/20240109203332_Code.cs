using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class Code : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cartografias_Propriedades_PropriedadeId",
                table: "Cartografias");

            migrationBuilder.DropColumn(
                name: "Propriedades",
                table: "Cartografias");

            migrationBuilder.AlterColumn<int>(
                name: "PropriedadeId",
                table: "Cartografias",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cartografias_Propriedades_PropriedadeId",
                table: "Cartografias",
                column: "PropriedadeId",
                principalTable: "Propriedades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cartografias_Propriedades_PropriedadeId",
                table: "Cartografias");

            migrationBuilder.AlterColumn<int>(
                name: "PropriedadeId",
                table: "Cartografias",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Propriedades",
                table: "Cartografias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Cartografias_Propriedades_PropriedadeId",
                table: "Cartografias",
                column: "PropriedadeId",
                principalTable: "Propriedades",
                principalColumn: "Id");
        }
    }
}
