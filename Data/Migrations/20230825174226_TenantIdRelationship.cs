using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class TenantIdRelationship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Industria",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Industria",
                table: "Produtores");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Solicitacao",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Propriedades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Produtores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Industrias",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Imagem = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NomeResumido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cnpj = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Industrias", x => x.TenantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Solicitacao_TenantId",
                table: "Solicitacao",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Propriedades_TenantId",
                table: "Propriedades",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Produtores_TenantId",
                table: "Produtores",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Industrias_TenantId",
                table: "AspNetUsers",
                column: "TenantId",
                principalTable: "Industrias",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Produtores_Industrias_TenantId",
                table: "Produtores",
                column: "TenantId",
                principalTable: "Industrias",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Propriedades_Industrias_TenantId",
                table: "Propriedades",
                column: "TenantId",
                principalTable: "Industrias",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitacao_Industrias_TenantId",
                table: "Solicitacao",
                column: "TenantId",
                principalTable: "Industrias",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Industrias_TenantId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Produtores_Industrias_TenantId",
                table: "Produtores");

            migrationBuilder.DropForeignKey(
                name: "FK_Propriedades_Industrias_TenantId",
                table: "Propriedades");

            migrationBuilder.DropForeignKey(
                name: "FK_Solicitacao_Industrias_TenantId",
                table: "Solicitacao");

            migrationBuilder.DropTable(
                name: "Industrias");

            migrationBuilder.DropIndex(
                name: "IX_Solicitacao_TenantId",
                table: "Solicitacao");

            migrationBuilder.DropIndex(
                name: "IX_Propriedades_TenantId",
                table: "Propriedades");

            migrationBuilder.DropIndex(
                name: "IX_Produtores_TenantId",
                table: "Produtores");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Solicitacao");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Produtores");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "Industria",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Industria",
                table: "Produtores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
