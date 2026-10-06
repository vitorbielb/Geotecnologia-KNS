using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// Até onde a verificação foi, no espaço e no tempo.
    /// </summary>
    /// <remarks>
    /// Nasceu do pior defeito que este sistema teve. Um imóvel em Amaralina, em
    /// Goiás, saiu da análise sem sobreposição de desmatamento — e o MapBiomas
    /// mostrava três alertas dentro do perímetro, 17,49 ha, detectados em 2019
    /// e 2020.
    ///
    /// A conta do cruzamento estava certa. Errada estava a guarda de cobertura,
    /// um nível acima: ela perguntava "existe camada deste tipo?", respondido
    /// uma vez para o país inteiro. O PRODES da Amazônia respondia que sim para
    /// uma fazenda do Cerrado, a regra se dizia avaliada, e o laudo liberava
    /// por uma verificação que nunca houve. Eram 334 mil imóveis da base em
    /// Goiás e Mato Grosso do Sul, todos fora do alcance das camadas de
    /// desmatamento carregadas.
    ///
    /// Numa amostra de 36 imóveis dos seis estados, 12 tinham desmatamento real
    /// dentro do perímetro e o sistema enxergava 2.
    ///
    /// Estes testes fixam as duas perguntas que passaram a ser feitas por
    /// imóvel, e não por país: a camada chega <b>aqui</b>, e alcança o
    /// <b>período</b> que a regra diz examinar.
    /// </remarks>
    public class AbrangenciaDaCamadaTests
    {
        private const string Car = "GO-5200829-E7A4513DC342419A992C0549E41EC8A9";

        /// <summary>Todos os tipos geográficos, como se tudo cobrisse o imóvel.</summary>
        private static IReadOnlyList<TipoCamada> TodosOsTipos =>
            PoliticaAnalise.Padrao().Regras
                .Where(r => r.EhGeografica)
                .Select(r => r.Tipo)
                .Distinct()
                .ToList();

        /// <summary>Os tipos acima, menos o desmatamento consolidado.</summary>
        private static IReadOnlyList<TipoCamada> SemDesmatamento =>
            TodosOsTipos.Where(t => t != TipoCamada.DesmatamentoConsolidado).ToList();

        private static ResultadoAvaliacao Avaliar(
            IReadOnlyList<TipoCamada> cobrem,
            IReadOnlyList<TipoCamada>? existem = null,
            IReadOnlyDictionary<TipoCamada, int?>? desde = null) =>
            new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(
                    Car, 2203.71, Array.Empty<Sobreposicao>(), DateTime.UtcNow,
                    cobrem, desde, existem ?? cobrem),
                PoliticaAnalise.Padrao());

        private static RegraNaoAvaliada Regra(ResultadoAvaliacao resultado, string codigo) =>
            resultado.NaoAvaliadas.Single(r => r.CodigoRegra == codigo);

        private static Dictionary<TipoCamada, int?> Desde(TipoCamada tipo, int? ano) =>
            new() { [tipo] = ano };

        [Fact]
        public void CamadaQueNaoAlcancaOImovel_NaoDeveContarComoAvaliada()
        {
            // O caso de Amaralina: o PRODES existe, tem 48 mil polígonos, e
            // nenhum deles em Goiás.
            var resultado = Avaliar(SemDesmatamento, existem: TodosOsTipos);

            Regra(resultado, "DES-001").Motivo.Should().Be(MotivoNaoAvaliada.ForaDaAbrangencia);
        }

        [Fact]
        public void CamadaQueNaoAlcancaOImovel_NaoDeveMandarCarregarArquivo()
        {
            // "Carregue a camada" é a instrução errada aqui: a camada está
            // carregada. Falta a do bioma certo, e isso se resolve noutro lugar.
            var explicacao = Regra(Avaliar(SemDesmatamento, TodosOsTipos), "DES-001").Explicacao;

            explicacao.Should().Contain("cobre a região");
            explicacao.Should().NotContain("carregada");
        }

        [Fact]
        public void NenhumaCamadaDoTipo_DeveContinuarDizendoQueFaltaCarregar()
        {
            // A distinção só vale se o outro lado dela continuar funcionando.
            var resultado = Avaliar(SemDesmatamento);

            Regra(resultado, "DES-001").Motivo.Should().Be(MotivoNaoAvaliada.CamadaAusente);
            Regra(resultado, "DES-001").Explicacao.Should().Contain("está carregada");
        }

        [Fact]
        public void CamadaForaDaAbrangencia_NaoDeveLiberarOImovel()
        {
            // O que o defeito produzia: veredito de liberação apoiado numa
            // regra que ninguém aplicou.
            Avaliar(SemDesmatamento, TodosOsTipos).Status.Should().NotBe(Status.Liberado);
        }

        [Fact]
        public void CamadaQueComecaDepoisDoCorteDaRegra_NaoDeveContarComoAvaliada()
        {
            // A DES-001 se chama "desmatamento consolidado a partir de 2008" e
            // era avaliada contra um PRODES que só tinha o ano de 2024.
            var regra = Regra(
                Avaliar(TodosOsTipos, desde: Desde(TipoCamada.DesmatamentoConsolidado, 2024)),
                "DES-001");

            regra.Motivo.Should().Be(MotivoNaoAvaliada.PeriodoNaoCoberto);
            regra.Explicacao.Should().Contain("2008").And.Contain("2024");
        }

        [Theory]
        [InlineData(2008)]
        [InlineData(2000)]
        public void CamadaQueAlcancaOCorte_DeveContarComoAvaliada(int inicio)
        {
            // Cobrir exatamente o corte, ou mais do que ele, não é falta.
            Avaliar(TodosOsTipos, desde: Desde(TipoCamada.DesmatamentoConsolidado, inicio))
                .NaoAvaliadas.Should().NotContain(r => r.CodigoRegra == "DES-001");
        }

        [Fact]
        public void CamadaSemRecorteTemporal_DeveContarComoAvaliada()
        {
            // Ano nulo é "não tem recorte", e não "começa no ano zero".
            Avaliar(TodosOsTipos, desde: Desde(TipoCamada.DesmatamentoConsolidado, null))
                .NaoAvaliadas.Should().NotContain(r => r.CodigoRegra == "DES-001");
        }

        [Fact]
        public void RegraSemCorteTemporal_NaoDeveSerAfetadaPeloInicioDaCamada()
        {
            // ALE-001 não declara ano mínimo: alerta recente é o que ela
            // examina, e uma camada que começa este ano a atende.
            Avaliar(TodosOsTipos, desde: Desde(TipoCamada.AlertaDesmatamento, DateTime.UtcNow.Year))
                .NaoAvaliadas.Should().NotContain(r => r.CodigoRegra == "ALE-001");
        }

        [Fact]
        public void ForaDaAbrangenciaEPeriodoCurto_DeveRelatarAAbrangencia()
        {
            // Onde a camada não chega, o período dela não diz nada — e mandar
            // carregar os anos que faltam seria a instrução errada.
            Regra(
                Avaliar(SemDesmatamento, TodosOsTipos,
                        Desde(TipoCamada.DesmatamentoConsolidado, 2024)),
                "DES-001")
                .Motivo.Should().Be(MotivoNaoAvaliada.ForaDaAbrangencia);
        }

        [Fact]
        public void SemInformacaoDeCobertura_DeveSeComportarComoAntes()
        {
            // Chamadas antigas não passam os dois campos novos. Elas não podem
            // passar a relatar falta de cobertura por causa disso — seria
            // inventar uma lacuna onde ninguém mediu nenhuma.
            var resultado = new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(
                    Car, 1000, Array.Empty<Sobreposicao>(), DateTime.UtcNow, TodosOsTipos),
                PoliticaAnalise.Padrao());

            resultado.NaoAvaliadas.Should().NotContain(r =>
                r.Motivo == MotivoNaoAvaliada.ForaDaAbrangencia ||
                r.Motivo == MotivoNaoAvaliada.PeriodoNaoCoberto);
        }
    }
}
