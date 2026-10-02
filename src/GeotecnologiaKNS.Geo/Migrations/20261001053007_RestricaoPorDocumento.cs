using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    /// <inheritdoc />
    public partial class RestricaoPorDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restricao_documento",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    nome_titular = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    referencia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    data_restricao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    tem_geometria = table.Column<bool>(type: "boolean", nullable: false),
                    carregado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restricao_documento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_restricao_documento",
                schema: "geo",
                table: "restricao_documento",
                columns: new[] { "documento", "tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restricao_documento",
                schema: "geo");
        }
    }
}
