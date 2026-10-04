using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;
using NetTopologySuite.Geometries;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// A conferência de que a geometria carregada está onde deveria.
    /// </summary>
    /// <remarks>
    /// Nasceu de um defeito que passou silencioso e por pouco não foi entregue.
    /// A base do CAR baixada do SICAR em SHAPE-ZIP veio com latitude e
    /// longitude trocadas: a partir do WFS 1.1.0 o servidor honra a ordem de
    /// eixos declarada no EPSG:4674, que é latitude primeiro. O GeoJSON vem
    /// certo nas duas versões, o que torna o problema ainda mais fácil de não
    /// ver.
    ///
    /// Um milhão e duzentos mil imóveis foram parar no meio do Atlântico. Nada
    /// falhou. O cruzamento simplesmente não encontrava nada, e toda análise
    /// devolvia "nenhuma sobreposição" — laudo limpo para todo fornecedor, que
    /// é o pior resultado possível e o que mais se parece com sucesso.
    /// </remarks>
    public class DentroDoBrasilTests
    {
        private static readonly GeometryFactory Fabrica = new();

        private static Geometry Ponto(double longitude, double latitude) =>
            Fabrica.CreatePoint(new Coordinate(longitude, latitude));

        private static Geometry Caixa(double minX, double minY, double maxX, double maxY) =>
            Fabrica.ToGeometry(new Envelope(minX, maxX, minY, maxY));

        [Theory]
        [InlineData(-55.68, -11.43)]   // Sinop/MT
        [InlineData(-48.78, -1.76)]    // Abaetetuba/PA
        [InlineData(-49.41, -12.68)]   // Araguaçu/TO
        [InlineData(-60.02, -3.10)]    // Manaus/AM
        [InlineData(-53.10, -33.69)]   // Chuí/RS, extremo sul
        [InlineData(-60.73, 5.19)]     // Roraima, extremo norte
        [InlineData(-34.79, -7.15)]    // Ponta do Seixas/PB, extremo leste
        public void GeometriaNoBrasil_DeveSerAceita(double longitude, double latitude)
        {
            Geometrias.DentroDoBrasil(Ponto(longitude, latitude)).Should().BeTrue();
        }

        [Fact]
        public void CoordenadasTrocadas_DevemSerRecusadas()
        {
            // O caso exato do SHAPE-ZIP em WFS 1.1.0: Abaetetuba com a
            // longitude no lugar da latitude cai no Atlântico, perto da África.
            Geometrias.DentroDoBrasil(Ponto(-1.76, -48.78)).Should().BeFalse();
        }

        [Theory]
        [InlineData(0, 0)]              // golfo da Guiné, o clássico do SRID errado
        [InlineData(-74.0, 40.7)]       // Nova York
        [InlineData(2.35, 48.86)]       // Paris
        public void GeometriaForaDoBrasil_DeveSerRecusada(double longitude, double latitude)
        {
            Geometrias.DentroDoBrasil(Ponto(longitude, latitude)).Should().BeFalse();
        }

        [Fact]
        public void PaisVizinhoColado_CaiDentroDaCaixa_EIssoEstaCerto()
        {
            // Buenos Aires está a meio grau do extremo sul do Brasil e cai
            // dentro do retângulo. A guarda é plausibilidade, não contorno do
            // país: ela existe para pegar eixo trocado e sistema de coordenadas
            // errado, que erram por dezenas de graus, não por meio.
            //
            // Apertar a caixa até excluir o vizinho recusaria imóvel legítimo de
            // fronteira, que é o erro caro — este teste está aqui para que
            // ninguém tente "melhorar" isso sem saber o que está trocando.
            Geometrias.DentroDoBrasil(Ponto(-58.38, -34.60)).Should().BeTrue();
        }

        [Fact]
        public void GeometriaQueAtravessaAFronteira_DeveSerRecusada()
        {
            // Metade dentro, metade fora: não dá para afirmar que está no lugar
            // certo, e aceitar esconderia um arquivo meio deslocado.
            Geometrias.DentroDoBrasil(Caixa(-80, -10, -70, -5)).Should().BeFalse();
        }

        [Fact]
        public void GeometriaVazia_DeveSerRecusada()
        {
            Geometrias.DentroDoBrasil((Geometry?)null).Should().BeFalse();
            Geometrias.DentroDoBrasil(Fabrica.CreatePoint()).Should().BeFalse();
        }

        [Fact]
        public void ExtensaoNula_DeveSerRecusada()
        {
            Geometrias.DentroDoBrasil((Envelope?)null).Should().BeFalse();
            Geometrias.DentroDoBrasil(new Envelope()).Should().BeFalse();
        }
    }
}
