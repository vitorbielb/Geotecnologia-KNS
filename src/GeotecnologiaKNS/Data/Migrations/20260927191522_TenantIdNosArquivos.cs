using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class TenantIdNosArquivos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PropriedadesArquivos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProdutoresArquivos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CartografiasArquivos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AnalisesArquivos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // A coluna nasce com zero, e zero não casa com nenhum tenant — sem
            // este preenchimento todo documento já enviado sumiria da interface.
            // O valor vem do registro-pai, que sempre teve o tenant correto.
            migrationBuilder.Sql(@"
                UPDATE a SET a.TenantId = p.TenantId
                FROM ProdutoresArquivos a
                INNER JOIN Produtores p ON p.Id = a.ProdutorId;");

            migrationBuilder.Sql(@"
                UPDATE a SET a.TenantId = p.TenantId
                FROM PropriedadesArquivos a
                INNER JOIN Propriedades p ON p.Id = a.PropriedadeId;");

            migrationBuilder.Sql(@"
                UPDATE a SET a.TenantId = s.TenantId
                FROM AnalisesArquivos a
                INNER JOIN Solicitacao s ON s.Id = a.SolicitacaoId;");

            migrationBuilder.Sql(@"
                UPDATE a SET a.TenantId = c.TenantId
                FROM CartografiasArquivos a
                INNER JOIN Cartografias c ON c.Id = a.CartografiaId;");

            migrationBuilder.CreateIndex(
                name: "IX_PropriedadesArquivos_TenantId",
                table: "PropriedadesArquivos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutoresArquivos_TenantId",
                table: "ProdutoresArquivos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CartografiasArquivos_TenantId",
                table: "CartografiasArquivos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalisesArquivos_TenantId",
                table: "AnalisesArquivos",
                column: "TenantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropriedadesArquivos_TenantId",
                table: "PropriedadesArquivos");

            migrationBuilder.DropIndex(
                name: "IX_ProdutoresArquivos_TenantId",
                table: "ProdutoresArquivos");

            migrationBuilder.DropIndex(
                name: "IX_CartografiasArquivos_TenantId",
                table: "CartografiasArquivos");

            migrationBuilder.DropIndex(
                name: "IX_AnalisesArquivos_TenantId",
                table: "AnalisesArquivos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PropriedadesArquivos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProdutoresArquivos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CartografiasArquivos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AnalisesArquivos");
        }
    }
}
