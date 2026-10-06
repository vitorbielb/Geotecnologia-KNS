using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// A conferência do que chegou contra o que a origem diz ter.
    /// </summary>
    /// <remarks>
    /// Existe por causa do defeito mais caro que esta base teve. O GeoServer do
    /// INPE limita cada requisição a 50.000 feições e, quando o pedido passa
    /// disso, devolve as primeiras 50.000 com <b>200 OK</b>. Nada falha: o zip
    /// é válido, o shapefile abre, o importador grava, a troca versionada
    /// publica. O PRODES da Amazônia entrou no ar com 50.000 polígonos de
    /// 802.277 — 6% — e não houve um registro de erro em lugar nenhum.
    ///
    /// A conferência de encolhimento não pegaria: ela compara com a versão
    /// anterior, e na primeira carga não há anterior. Era cega exatamente onde
    /// precisava enxergar.
    ///
    /// Esta aqui compara com quem sabe a resposta — a própria origem.
    /// </remarks>
    public class ConferenciaContraOrigemTests
    {
        private const string Chave = "prodes-amazonia";

        private static Action Conferir(int? esperado, int lidos) =>
            () => GuardaDeCarga.ConferirContraOrigem(Chave, esperado, lidos);

        [Fact]
        public void CargaTruncadaPeloTetoDoServidor_DeveSerRecusada()
        {
            // Os números reais do dia em que isso aconteceu.
            Conferir(802_277, 50_000).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ARecusa_DeveDizerOsDoisNumerosEOQueFazer()
        {
            // Quem lê precisa saber que faltou, quanto faltou e para onde ir.
            // "Carga recusada" sozinho manda a pessoa procurar no escuro.
            Conferir(802_277, 50_000).Should().Throw<InvalidOperationException>()
                .WithMessage("*802.277*")
                .WithMessage("*50.000*")
                .WithMessage("*páginas*");
        }

        [Fact]
        public void CargaCompleta_DevePassar()
        {
            Conferir(25_357, 25_357).Should().NotThrow();
        }

        [Fact]
        public void FeicaoPublicadaEntreAContagemEODownload_NaoDeveRecusar()
        {
            // A contagem e o download são duas requisições. Uma feição que
            // entre entre elas aparece como diferença, e recusar por isso
            // trocaria uma recarga boa por nenhuma.
            Conferir(100_000, 99_999).Should().NotThrow();
        }

        [Theory]
        [InlineData(100_000, 99_000)]   // exatamente a margem
        [InlineData(100_000, 100_500)]  // a origem cresceu entre as duas requisições
        public void DiferencaDentroDaMargem_NaoDeveRecusar(int esperado, int lidos)
        {
            Conferir(esperado, lidos).Should().NotThrow();
        }

        [Fact]
        public void PerdaAlemDaMargem_DeveSerRecusada()
        {
            // Truncar corta dezenas de por cento, não frações. Logo abaixo da
            // margem já é sinal de que algo cortou a resposta.
            Conferir(100_000, 98_900).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void OrigemQueNaoSabeResponder_NaoDeveImpedirACarga()
        {
            // Nem toda origem é WFS, e nem todo WFS aceita resultType=hits.
            // Sem contagem não há conferência — mas trocar uma recarga boa por
            // nenhuma porque a rede de proteção não pôde ser armada seria o
            // remédio pior que a doença. O aviso fica no registro.
            Conferir(null, 50_000).Should().NotThrow();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ContagemSemSentido_DeveSerIgnorada(int esperado)
        {
            // Zero não é "a origem está vazia": é a origem não tendo respondido
            // direito. Recusar por causa disso derrubaria cargas legítimas.
            Conferir(esperado, 1_000).Should().NotThrow();
        }

        [Fact]
        public void CargaVazia_ContraOrigemQueTemCoisas_DeveSerRecusada()
        {
            // O caso extremo do truncamento: o download inteiro se perdeu.
            Conferir(802_277, 0).Should().Throw<InvalidOperationException>();
        }
    }
}
