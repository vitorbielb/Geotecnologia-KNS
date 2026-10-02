using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    public partial class InicialGeo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "geo");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "carga_base_car",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    arquivo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    hash_arquivo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    iniciada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concluida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    registros_lidos = table.Column<int>(type: "integer", nullable: false),
                    registros_gravados = table.Column<int>(type: "integer", nullable: false),
                    registros_descartados = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carga_base_car", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "imovel_car",
                schema: "geo",
                columns: table => new
                {
                    codigo_car = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    perimetro = table.Column<Geometry>(type: "geometry(Geometry,4326)", nullable: false),
                    centroide = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    area_ha = table.Column<double>(type: "double precision", nullable: true),
                    area_calculada_ha = table.Column<double>(type: "double precision", nullable: true),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    situacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    atualizado_em_origem = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    carga_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imovel_car", x => x.codigo_car);
                    table.ForeignKey(
                        name: "FK_imovel_car_carga_base_car_carga_id",
                        column: x => x.carga_id,
                        principalSchema: "geo",
                        principalTable: "carga_base_car",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imovel_car_carga_id",
                schema: "geo",
                table: "imovel_car",
                column: "carga_id");

            migrationBuilder.CreateIndex(
                name: "IX_imovel_car_perimetro",
                schema: "geo",
                table: "imovel_car",
                column: "perimetro")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_imovel_car_uf",
                schema: "geo",
                table: "imovel_car",
                column: "uf");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imovel_car",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "carga_base_car",
                schema: "geo");
        }
    }
}
