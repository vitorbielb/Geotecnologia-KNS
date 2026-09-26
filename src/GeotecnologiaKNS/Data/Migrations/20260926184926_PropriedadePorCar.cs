using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <summary>
    /// Adiciona os campos derivados do CAR e alinha o banco ao modelo.
    /// </summary>
    /// <remarks>
    /// Esta migration carrega duas coisas: os campos novos do CAR e o drift
    /// acumulado desde a última migration (janeiro/2024) — limites de tamanho
    /// declarados no modelo que nunca chegaram ao banco, e a troca de lado do
    /// relacionamento Produtor/Solicitação.
    ///
    /// O scaffold original derrubava Produtores.SolicitacoesId antes de existir
    /// destino para o dado, e reduzia o tamanho de colunas sem tratar os valores
    /// existentes — o ALTER falharia com "String or binary data would be
    /// truncated". Como a aplicação roda MigrateAsync() no startup, isso
    /// quebraria a subida. As operações foram reordenadas e os dados são
    /// migrados ou truncados explicitamente antes de cada ALTER.
    /// </remarks>
    public partial class PropriedadePorCar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Produtor/Solicitação: cria o destino, move o dado, só então remove a origem ---

            migrationBuilder.AddColumn<int>(
                name: "ProdutorId",
                table: "Solicitacao",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE s
                SET s.ProdutorId = p.Id
                FROM Solicitacao s
                INNER JOIN Produtores p ON p.SolicitacoesId = s.Id
                WHERE s.ProdutorId IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Produtores_Solicitacao_SolicitacoesId",
                table: "Produtores");

            migrationBuilder.DropIndex(
                name: "IX_Produtores_SolicitacoesId",
                table: "Produtores");

            migrationBuilder.DropColumn(
                name: "SolicitacoesId",
                table: "Produtores");

            // --- Limites de tamanho: trunca antes de alterar, senão o ALTER falha ---

            migrationBuilder.Sql("UPDATE Solicitacao SET Solicitante = LEFT(Solicitante, 150) WHERE LEN(Solicitante) > 150;");
            migrationBuilder.Sql("UPDATE Solicitacao SET Analista = LEFT(Analista, 150) WHERE LEN(Analista) > 150;");
            migrationBuilder.Sql("UPDATE Solicitacao SET Observacao = LEFT(Observacao, 2000) WHERE LEN(Observacao) > 2000;");
            migrationBuilder.Sql("UPDATE Solicitacao SET Parecer = LEFT(Parecer, 2000) WHERE LEN(Parecer) > 2000;");
            migrationBuilder.Sql("UPDATE Produtores SET Cpf = LEFT(Cpf, 18) WHERE LEN(Cpf) > 18;");
            migrationBuilder.Sql("UPDATE Propriedades SET NomePropriedade = LEFT(NomePropriedade, 100) WHERE LEN(NomePropriedade) > 100;");

            migrationBuilder.AlterColumn<string>(
                name: "Solicitante",
                table: "Solicitacao",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Parecer",
                table: "Solicitacao",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Observacao",
                table: "Solicitacao",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Analista",
                table: "Solicitacao",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "Produtores",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "NomePropriedade",
                table: "Propriedades",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            // --- Campos derivados da base do CAR ---

            migrationBuilder.AddColumn<string>(
                name: "CodigoCar",
                table: "Propriedades",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PerimetroGeoJson",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerimetroOrigem",
                table: "Propriedades",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PerimetroAtualizadoEm",
                table: "Propriedades",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SituacaoCar",
                table: "Propriedades",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Aproveita o CAR já digitado no campo legado quando ele tem o formato
            // esperado (UF-CódigoIBGE-Hash), para não obrigar a redigitar tudo.
            migrationBuilder.Sql(@"
                UPDATE Propriedades
                SET CodigoCar = UPPER(LTRIM(RTRIM(CadastroAmbientalRural)))
                WHERE CodigoCar = ''
                  AND CadastroAmbientalRural LIKE '[A-Za-z][A-Za-z]-[0-9][0-9][0-9][0-9][0-9][0-9][0-9]-%'
                  AND LEN(LTRIM(RTRIM(CadastroAmbientalRural))) <= 100;");

            migrationBuilder.CreateIndex(
                name: "IX_Propriedades_CodigoCar",
                table: "Propriedades",
                column: "CodigoCar");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitacao_ProdutorId",
                table: "Solicitacao",
                column: "ProdutorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitacao_Produtores_ProdutorId",
                table: "Solicitacao",
                column: "ProdutorId",
                principalTable: "Produtores",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Solicitacao_Produtores_ProdutorId",
                table: "Solicitacao");

            migrationBuilder.DropIndex(
                name: "IX_Solicitacao_ProdutorId",
                table: "Solicitacao");

            migrationBuilder.DropIndex(
                name: "IX_Propriedades_CodigoCar",
                table: "Propriedades");

            migrationBuilder.AddColumn<int>(
                name: "SolicitacoesId",
                table: "Produtores",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE p
                SET p.SolicitacoesId = s.Id
                FROM Produtores p
                INNER JOIN Solicitacao s ON s.ProdutorId = p.Id;");

            migrationBuilder.DropColumn(
                name: "ProdutorId",
                table: "Solicitacao");

            migrationBuilder.DropColumn(
                name: "CodigoCar",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "PerimetroAtualizadoEm",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "PerimetroGeoJson",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "PerimetroOrigem",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "SituacaoCar",
                table: "Propriedades");

            migrationBuilder.AlterColumn<string>(
                name: "Solicitante",
                table: "Solicitacao",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Parecer",
                table: "Solicitacao",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Observacao",
                table: "Solicitacao",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Analista",
                table: "Solicitacao",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NomePropriedade",
                table: "Propriedades",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "Produtores",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(18)",
                oldMaxLength: 18);

            migrationBuilder.CreateIndex(
                name: "IX_Produtores_SolicitacoesId",
                table: "Produtores",
                column: "SolicitacoesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Produtores_Solicitacao_SolicitacoesId",
                table: "Produtores",
                column: "SolicitacoesId",
                principalTable: "Solicitacao",
                principalColumn: "Id");
        }
    }
}
