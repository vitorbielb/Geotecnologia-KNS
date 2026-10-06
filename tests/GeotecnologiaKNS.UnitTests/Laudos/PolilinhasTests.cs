using FluentAssertions;
using GeotecnologiaKNS.Laudos;

namespace GeotecnologiaKNS.UnitTests.Laudos
{
    /// <summary>
    /// A codificação das geometrias para o mapa do laudo.
    /// </summary>
    /// <remarks>
    /// O endereço da imagem estática tem limite de tamanho, e um perímetro do
    /// CAR escrito em coordenadas soltas passa dele sozinho: cada ponto custa
    /// mais de vinte caracteres, codificado custa cinco.
    ///
    /// Errar aqui não quebra nada visivelmente — o mapa some do laudo e o
    /// documento sai dizendo que o mapa não pôde ser gerado, que é o
    /// comportamento correto para falha de rede e o errado para defeito de
    /// código. Por isso o teste.
    /// </remarks>
    public class PolilinhasTests
    {
        /// <summary>
        /// Exemplo da documentação do algoritmo, com o resultado conhecido.
        /// </summary>
        /// <remarks>
        /// Vale mais que qualquer caso inventado: se bater com o exemplo
        /// oficial, a implementação é a mesma que o serviço espera receber.
        /// </remarks>
        [Fact]
        public void Codificacao_DeveBaterComOExemploOficial()
        {
            // (38.5, -120.2), (40.7, -120.95), (43.252, -126.453)
            const string geoJson =
                """{"type":"Polygon","coordinates":[[[-120.2,38.5],[-120.95,40.7],[-126.453,43.252]]]}""";

            var aneis = Polilinhas.De(geoJson);

            aneis.Should().ContainSingle();
            Uri.UnescapeDataString(aneis[0]).Should().Be("_p~iF~ps|U_ulLnnqC_mqNvxq`@");
        }

        [Fact]
        public void MultiPolygon_DeveDevolverUmAnelPorPoligono()
        {
            // A base do CAR grava MultiPolygon; o laudo precisa desenhar todos
            // os pedaços, não só o primeiro.
            const string geoJson =
                """
                {"type":"MultiPolygon","coordinates":[
                  [[[-52.47,-6.52],[-52.46,-6.52],[-52.46,-6.51],[-52.47,-6.51],[-52.47,-6.52]]],
                  [[[-52.40,-6.60],[-52.39,-6.60],[-52.39,-6.59],[-52.40,-6.59],[-52.40,-6.60]]]]}
                """;

            Polilinhas.De(geoJson).Should().HaveCount(2);
        }

        [Fact]
        public void PoligonoComBuraco_DeveDevolverOsDoisAneis()
        {
            const string geoJson =
                """
                {"type":"Polygon","coordinates":[
                  [[-52.5,-6.6],[-52.3,-6.6],[-52.3,-6.4],[-52.5,-6.4],[-52.5,-6.6]],
                  [[-52.45,-6.55],[-52.40,-6.55],[-52.40,-6.50],[-52.45,-6.50],[-52.45,-6.55]]]}
                """;

            Polilinhas.De(geoJson).Should().HaveCount(2);
        }

        [Fact]
        public void AnelComMenosDeTresPontos_DeveSerDescartado()
        {
            // Não é polígono, e o serviço de mapas recusaria o endereço inteiro
            // por causa dele — levando junto o perímetro que estava correto.
            const string geoJson =
                """{"type":"Polygon","coordinates":[[[-52.47,-6.52],[-52.46,-6.52]]]}""";

            Polilinhas.De(geoJson).Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("isso não é json")]
        [InlineData("{}")]
        [InlineData("""{"type":"Point","coordinates":[-52.47,-6.52]}""")]
        public void EntradaInvalida_NaoDeveExplodir(string geoJson)
        {
            // O laudo sai sem mapa, não sem laudo.
            Polilinhas.De(geoJson).Should().BeEmpty();
        }

        [Fact]
        public void Codificacao_DeveEncolherOBastanteParaCaberNoEndereco()
        {
            // Um perímetro do CAR tem dezenas de vértices. Em coordenadas
            // soltas ele sozinho estoura o limite do endereço.
            var pontos = Enumerable.Range(0, 80)
                .Select(i => $"[{-52.47 + i * 0.0001:F6},{-6.52 + i * 0.0001:F6}]");

            var geoJson = $$"""{"type":"Polygon","coordinates":[[{{string.Join(",", pontos)}}]]}""";

            var codificado = Polilinhas.De(geoJson).Single();

            // Sete vezes menor no perímetro real medido; metade é o piso que
            // garante a compressão sem depender do formato exato do GeoJSON.
            codificado.Length.Should().BeLessThan(geoJson.Length / 2);
        }
    }
}
