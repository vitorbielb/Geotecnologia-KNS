using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    /// <inheritdoc />
    public partial class VersaoDeCamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_feicao_referencia_camada_id",
                schema: "geo",
                table: "feicao_referencia");

            migrationBuilder.AddColumn<int>(
                name: "versao",
                schema: "geo",
                table: "feicao_referencia",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "periodicidade_dias",
                schema: "geo",
                table: "camada_referencia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "versao_atual",
                schema: "geo",
                table: "camada_referencia",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_feicao_camada_versao",
                schema: "geo",
                table: "feicao_referencia",
                columns: new[] { "camada_id", "versao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_feicao_camada_versao",
                schema: "geo",
                table: "feicao_referencia");

            migrationBuilder.DropColumn(
                name: "versao",
                schema: "geo",
                table: "feicao_referencia");

            migrationBuilder.DropColumn(
                name: "periodicidade_dias",
                schema: "geo",
                table: "camada_referencia");

            migrationBuilder.DropColumn(
                name: "versao_atual",
                schema: "geo",
                table: "camada_referencia");

            migrationBuilder.CreateIndex(
                name: "IX_feicao_referencia_camada_id",
                schema: "geo",
                table: "feicao_referencia",
                column: "camada_id");
        }
    }
}
