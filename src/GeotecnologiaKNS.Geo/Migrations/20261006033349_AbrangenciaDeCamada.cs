using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GeotecnologiaKNS.Geo.Migrations
{
    /// <inheritdoc />
    public partial class AbrangenciaDeCamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ano",
                schema: "geo",
                table: "feicao_referencia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Geometry>(
                name: "abrangencia",
                schema: "geo",
                table: "camada_referencia",
                type: "geometry(Geometry,4326)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cobre_desde_ano",
                schema: "geo",
                table: "camada_referencia",
                type: "integer",
                nullable: true);

            // As camadas já carregadas ficam com abrangência nula, e nula
            // significa cobertura nacional. Para as duas camadas regionais que
            // existiam isso é exatamente a afirmação falsa que esta migração
            // veio desfazer: o PRODES e o DETER da Amazônia responderiam
            // "cubro este imóvel" para uma fazenda de Goiás.
            //
            // Desligá-las é o que protege. Camada desligada faz a regra sair
            // como NÃO AVALIADA, que é a verdade até a recarga declarar o que
            // cada uma cobre; deixá-las ligadas mantém o laudo liberando por
            // omissão. Entre uma análise que diz menos e uma que afirma o que
            // não apurou, a que diz menos é a defensável.
            //
            // O PRODES ganha chave nova — "prodes-amazonia", sem o ano, porque
            // agora carrega de 2008 em diante — e por isso a entrada antiga não
            // volta sozinha: ela saiu do catálogo.
            migrationBuilder.Sql(@"
                UPDATE geo.camada_referencia
                   SET ativa = false
                 WHERE chave IN ('prodes-amazonia-2024', 'deter-amazonia')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ano",
                schema: "geo",
                table: "feicao_referencia");

            migrationBuilder.DropColumn(
                name: "abrangencia",
                schema: "geo",
                table: "camada_referencia");

            migrationBuilder.DropColumn(
                name: "cobre_desde_ano",
                schema: "geo",
                table: "camada_referencia");

            migrationBuilder.Sql(@"
                UPDATE geo.camada_referencia
                   SET ativa = true
                 WHERE chave IN ('prodes-amazonia-2024', 'deter-amazonia')");
        }
    }
}
