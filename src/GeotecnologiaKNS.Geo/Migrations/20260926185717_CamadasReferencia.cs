using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    public partial class CamadasReferencia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "camada_referencia",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ano_referencia = table.Column<int>(type: "integer", nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_feicoes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camada_referencia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feicao_referencia",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    camada_id = table.Column<int>(type: "integer", nullable: false),
                    geometria = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: false),
                    atributos = table.Column<string>(type: "jsonb", nullable: true),
                    rotulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feicao_referencia", x => x.id);
                    table.ForeignKey(
                        name: "FK_feicao_referencia_camada_referencia_camada_id",
                        column: x => x.camada_id,
                        principalSchema: "geo",
                        principalTable: "camada_referencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camada_referencia_chave",
                schema: "geo",
                table: "camada_referencia",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_camada_referencia_tipo",
                schema: "geo",
                table: "camada_referencia",
                column: "tipo");

            migrationBuilder.CreateIndex(
                name: "IX_feicao_referencia_camada_id",
                schema: "geo",
                table: "feicao_referencia",
                column: "camada_id");

            migrationBuilder.CreateIndex(
                name: "IX_feicao_referencia_geometria",
                schema: "geo",
                table: "feicao_referencia",
                column: "geometria")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feicao_referencia",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "camada_referencia",
                schema: "geo");
        }
    }
}
