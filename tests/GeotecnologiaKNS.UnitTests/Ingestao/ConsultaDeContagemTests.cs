using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// A URL de download transformada na mesma consulta pedindo só a contagem.
    /// </summary>
    /// <remarks>
    /// É a fundação da conferência contra a origem — e, por isso mesmo, o lugar
    /// onde um defeito não aparece. Se a transformação errar, a contagem volta
    /// nula, a conferência se desliga e a carga entra sem rede. Nada falha; só
    /// deixa de proteger.
    ///
    /// Foi o que aconteceu: a primeira versão tinha um erro de uma letra na
    /// expressão que lê o número da resposta. Nunca casava. Só apareceu porque
    /// alguém estava lendo o registro na hora em que a carga rodou.
    ///
    /// O número só serve para conferir se for exatamente o mesmo recorte do
    /// download. Por isso o que estes testes mais vigiam é o que <b>não</b>
    /// pode mudar na travessia: a camada e o filtro.
    /// </remarks>
    public class ConsultaDeContagemTests
    {
        private const string Download =
            "https://terrabrasilis.dpi.inpe.br/geoserver/ows?service=WFS&version=1.0.0" +
            "&request=GetFeature&typeName=prodes-cerrado-nb:yearly_deforestation" +
            "&outputFormat=SHAPE-ZIP&CQL_FILTER=year%20%3E%3D%202008";

        private static string Consulta() =>
            RecarregadorDeCamadas.ConsultaDeContagem(Download)!;

        [Fact]
        public void DeveManterOMesmoFiltro()
        {
            // Contar o conjunto inteiro e comparar com o download de um recorte
            // recusaria toda carga filtrada — e o PRODES inteiro é filtrado.
            Consulta().Should().Contain("CQL_FILTER=year%20%3E%3D%202008");
        }

        [Fact]
        public void DeveManterAMesmaCamada()
        {
            Consulta().Should().Contain("prodes-cerrado-nb:yearly_deforestation");
        }

        [Fact]
        public void DevePedirApenasAContagem()
        {
            Consulta().Should().Contain("resultType=hits");
        }

        [Fact]
        public void DeveSubirParaAVersaoQueEntendeContagem()
        {
            // resultType=hits não existe na 1.0.0. A troca é segura aqui porque
            // nenhuma geometria vem na resposta — não há eixo que a 1.1.0 em
            // diante pudesse inverter, que é o motivo de o download ser 1.0.0.
            var consulta = Consulta();

            consulta.Should().Contain("version=2.0.0");
            consulta.Should().NotContain("version=1.0.0");
        }

        [Fact]
        public void DeveUsarONomePluralDoParametroDeCamada()
        {
            // typeName na 1.0.0, typeNames na 2.0.0. Mandar o singular devolve
            // erro, e o erro vira contagem nula, que vira carga sem conferência.
            var consulta = Consulta();

            consulta.Should().Contain("typeNames=");
            consulta.Should().NotContain("typeName=prodes");
        }

        [Fact]
        public void NaoDevePedirOArquivo()
        {
            // Pedir SHAPE-ZIP junto com hits baixaria a camada inteira só para
            // contá-la — e o ponto da contagem é ser barata.
            Consulta().Should().NotContain("outputFormat");
        }

        [Fact]
        public void DevePreservarOEnderecoDoServidor()
        {
            Consulta().Should().StartWith("https://terrabrasilis.dpi.inpe.br/geoserver/ows?");
        }

        [Fact]
        public void DeveDescartarPaginacaoDeUmaTentativaAnterior()
        {
            // Contar a partir da página três responderia o tamanho do resto, e
            // a carga inteira seria comparada contra ele.
            var comPaginacao = Download + "&sortBy=fid&startIndex=100000&maxFeatures=50000";

            var consulta = RecarregadorDeCamadas.ConsultaDeContagem(comPaginacao)!;

            consulta.Should().NotContain("startIndex");
            consulta.Should().NotContain("maxFeatures");
            consulta.Should().NotContain("sortBy");
        }

        [Theory]
        [InlineData("https://cnuc-backend.mma.gov.br/api/v1/downloadGeo")]
        [InlineData("https://certificacao.incra.gov.br/csv_shp/zip/Assentamento%20Brasil.zip")]
        [InlineData("https://stibamadadosabertosprd.blob.core.windows.net/dados/termo.csv")]
        public void OrigemQueNaoEhWfs_NaoDeveTerConsultaDeContagem(string url)
        {
            // Nem toda origem responde a essa pergunta. Devolver nulo faz a
            // carga seguir sem conferência — e o registro diz que seguiu.
            RecarregadorDeCamadas.ConsultaDeContagem(url).Should().BeNull();
        }
    }
}
