using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class Cartog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DataSolicitacao",
                table: "Solicitacao",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateTable(
                name: "Cartografias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Propriedades = table.Column<int>(type: "int", nullable: false),
                    PropriedadeId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataCartografia = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartografias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cartografias_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CartografiasArquivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartografiaId = table.Column<int>(type: "int", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dados = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartografiasArquivos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartografiasArquivos_Cartografias_CartografiaId",
                        column: x => x.CartografiaId,
                        principalTable: "Cartografias",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_PropriedadeId",
                table: "Cartografias",
                column: "PropriedadeId");

            migrationBuilder.CreateIndex(
                name: "IX_CartografiasArquivos_CartografiaId",
                table: "CartografiasArquivos",
                column: "CartografiaId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartografiasArquivos");

            migrationBuilder.DropTable(
                name: "Cartografias");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DataSolicitacao",
                table: "Solicitacao",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
