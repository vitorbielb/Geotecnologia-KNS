using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    public partial class CoberturaDeMunicipios : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cobertura_municipio",
                schema: "geo",
                columns: table => new
                {
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    imoveis = table.Column<int>(type: "integer", nullable: false),
                    carga_id = table.Column<long>(type: "bigint", nullable: false),
                    coberto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cobertura_municipio", x => x.codigo_ibge);
                    table.ForeignKey(
                        name: "FK_cobertura_municipio_carga_base_car_carga_id",
                        column: x => x.carga_id,
                        principalSchema: "geo",
                        principalTable: "carga_base_car",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lacuna_cobertura",
                schema: "geo",
                columns: table => new
                {
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    tenant_id = table.Column<int>(type: "integer", nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ultimo_codigo_car = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consultas = table.Column<int>(type: "integer", nullable: false),
                    primeira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultima_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lacuna_cobertura", x => new { x.codigo_ibge, x.tenant_id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_cobertura_municipio_carga_id",
                schema: "geo",
                table: "cobertura_municipio",
                column: "carga_id");

            migrationBuilder.CreateIndex(
                name: "IX_cobertura_municipio_uf",
                schema: "geo",
                table: "cobertura_municipio",
                column: "uf");

            migrationBuilder.CreateIndex(
                name: "IX_lacuna_cobertura_ultima_em",
                schema: "geo",
                table: "lacuna_cobertura",
                column: "ultima_em");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cobertura_municipio",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "lacuna_cobertura",
                schema: "geo");
        }
    }
}
