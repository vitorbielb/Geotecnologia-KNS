using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    /// <inheritdoc />
    public partial class ListaRestritivaVersionada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_restricao_documento",
                schema: "geo",
                table: "restricao_documento");

            migrationBuilder.AddColumn<int>(
                name: "versao",
                schema: "geo",
                table: "restricao_documento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "lista_restritiva",
                schema: "geo",
                columns: table => new
                {
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    versao_atual = table.Column<int>(type: "integer", nullable: false),
                    total_registros = table.Column<int>(type: "integer", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    periodicidade_dias = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_restritiva", x => x.tipo);
                });

            migrationBuilder.CreateIndex(
                name: "ix_restricao_documento",
                schema: "geo",
                table: "restricao_documento",
                columns: new[] { "documento", "tipo", "versao" });

            // Sem isto, a consulta por documento — que agora casa pela versão
            // publicada — deixaria de encontrar as cem mil restrições já
            // carregadas, e as regras EMB-002 e TRB-001 passariam a dizer que
            // não havia lista para consultar. A versão das linhas existentes é
            // zero, então o registro da lista nasce apontando para zero.
            migrationBuilder.Sql(@"
                INSERT INTO geo.lista_restritiva
                    (tipo, nome, origem, versao_atual, total_registros, atualizada_em, periodicidade_dias)
                SELECT r.tipo,
                       CASE r.tipo
                           WHEN 1 THEN 'Termos de embargo por CPF/CNPJ'
                           WHEN 2 THEN 'Cadastro de Empregadores'
                           ELSE 'Lista restritiva'
                       END,
                       MIN(r.origem),
                       0,
                       COUNT(*),
                       MAX(r.carregado_em),
                       CASE r.tipo WHEN 1 THEN 7 WHEN 2 THEN 30 ELSE NULL END
                FROM geo.restricao_documento r
                GROUP BY r.tipo
                ON CONFLICT (tipo) DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lista_restritiva",
                schema: "geo");

            migrationBuilder.DropIndex(
                name: "ix_restricao_documento",
                schema: "geo",
                table: "restricao_documento");

            migrationBuilder.DropColumn(
                name: "versao",
                schema: "geo",
                table: "restricao_documento");

            migrationBuilder.CreateIndex(
                name: "ix_restricao_documento",
                schema: "geo",
                table: "restricao_documento",
                columns: new[] { "documento", "tipo" });
        }
    }
}
