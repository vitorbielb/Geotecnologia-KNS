using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class RemoveCartografia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Solicitacao_Cartografias_CartografiaId",
                table: "Solicitacao");

            migrationBuilder.DropForeignKey(
                name: "FK_Cartografias_CartografiasArquivos_CartografiaArquivoId",
                table: "Cartografias");

            migrationBuilder.DropTable(
                name: "CartografiasArquivos");

            migrationBuilder.DropTable(
                name: "Cartografias");

            migrationBuilder.DropIndex(
                name: "IX_Solicitacao_CartografiaId",
                table: "Solicitacao");

            migrationBuilder.DropColumn(
                name: "CartografiaId",
                table: "Solicitacao");

            // As permissões de cartografia continuariam gravadas nos papéis,
            // apontando para uma funcionalidade que não existe mais. São inertes
            // — nada as lê —, mas poluem a tela de permissões de quem for
            // depurar um acesso mais tarde.
            migrationBuilder.Sql(
                "DELETE FROM AspNetRoleClaims WHERE ClaimType LIKE 'Cartografia.%';");

            migrationBuilder.Sql(
                "DELETE FROM AspNetUserClaims WHERE ClaimType LIKE 'Cartografia.%';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CartografiaId",
                table: "Solicitacao",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cartografias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartografiaArquivoId = table.Column<int>(type: "int", nullable: true),
                    PropriedadeId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartografias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cartografias_Industrias_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Industrias",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cartografias_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartografiasArquivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartografiaId = table.Column<int>(type: "int", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dados = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DataCartografia = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                name: "IX_Solicitacao_CartografiaId",
                table: "Solicitacao",
                column: "CartografiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_CartografiaArquivoId",
                table: "Cartografias",
                column: "CartografiaArquivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_PropriedadeId",
                table: "Cartografias",
                column: "PropriedadeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cartografias_TenantId",
                table: "Cartografias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CartografiasArquivos_CartografiaId",
                table: "CartografiasArquivos",
                column: "CartografiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CartografiasArquivos_TenantId",
                table: "CartografiasArquivos",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitacao_Cartografias_CartografiaId",
                table: "Solicitacao",
                column: "CartografiaId",
                principalTable: "Cartografias",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Cartografias_CartografiasArquivos_CartografiaArquivoId",
                table: "Cartografias",
                column: "CartografiaArquivoId",
                principalTable: "CartografiasArquivos",
                principalColumn: "Id");
        }
    }
}
