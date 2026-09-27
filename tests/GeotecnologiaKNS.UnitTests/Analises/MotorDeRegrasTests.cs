using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    public class MotorDeRegrasTests
    {
        // Metodo_Cenario_ResultadoEsperado

        private const string Car = "MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        private static Sobreposicao Sobreposicao(
            TipoCamada tipo,
            double areaHa = 10,
            double percentual = 1,
            int? ano = null,
            string nome = "Camada de teste") =>
            new(
                CamadaChave: "teste",
                CamadaNome: nome,
                Tipo: tipo,
                Origem: "Origem de teste",
                AnoReferencia: ano,
                Rotulo: "Feição de teste",
                AtributosJson: null,
                AreaSobrepostaHa: areaHa,
                PercentualDoImovel: percentual);

        private static ResultadoCruzamento Cruzamento(params Sobreposicao[] sobreposicoes) =>
            new(Car, AreaImovelHa: 1000, sobreposicoes, DateTime.UtcNow);

        private static ResultadoAvaliacao Avaliar(params Sobreposicao[] sobreposicoes) =>
            new MotorDeRegras().Avaliar(Cruzamento(sobreposicoes), PoliticaAnalise.Padrao());

        [Fact]
        public void Avaliar_SemSobreposicao_DeveLiberar()
        {
            var resultado = Avaliar();

            resultado.Status.Should().Be(Status.Liberado);
            resultado.Achados.Should().BeEmpty();
            resultado.Parecer.Should().Contain("LIBERADO");
        }

        [Theory]
        [InlineData(TipoCamada.EmbargoAmbiental)]
        [InlineData(TipoCamada.TerraIndigena)]
        [InlineData(TipoCamada.TerritorioQuilombola)]
        [InlineData(TipoCamada.UnidadeConservacao)]
        public void Avaliar_SobreposicaoImpeditiva_DeveBloquear(TipoCamada tipo)
        {
            var resultado = Avaliar(Sobreposicao(tipo));

            resultado.Status.Should().Be(Status.Bloqueado);
            resultado.TemBloqueio.Should().BeTrue();
        }

        [Fact]
        public void Avaliar_AlertaDeDesmatamento_DeveGerarAlertaENaoBloqueio()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.AlertaDesmatamento));

            resultado.Status.Should().Be(Status.Alerta);
            resultado.TemBloqueio.Should().BeFalse();
        }

        [Fact]
        public void Avaliar_UmBloqueioEntreVariosAlertas_DeveBloquear()
        {
            var resultado = Avaliar(
                Sobreposicao(TipoCamada.AlertaDesmatamento),
                Sobreposicao(TipoCamada.OutroPerimetro),
                Sobreposicao(TipoCamada.TerraIndigena));

            resultado.Status.Should().Be(Status.Bloqueado, "um único impedimento basta para bloquear");
        }

        [Fact]
        public void Avaliar_VariosAchados_DeveOrdenarPelaGravidade()
        {
            var resultado = Avaliar(
                Sobreposicao(TipoCamada.AlertaDesmatamento, areaHa: 500),
                Sobreposicao(TipoCamada.TerraIndigena, areaHa: 1));

            resultado.Achados.First().Severidade.Should().Be(Severidade.Bloqueio,
                "o laudo precisa abrir com o achado que decide o veredito");
        }

        [Fact]
        public void Avaliar_DesmatamentoAnteriorAoCorteTemporal_NaoDeveBloquear()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.DesmatamentoConsolidado, ano: 2007));

            resultado.Status.Should().Be(Status.Liberado);
        }

        [Fact]
        public void Avaliar_DesmatamentoAPartirDoCorteTemporal_DeveBloquear()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.DesmatamentoConsolidado, ano: 2008));

            resultado.Status.Should().Be(Status.Bloqueado);
        }

        [Fact]
        public void Avaliar_DesmatamentoSemAnoDeReferencia_NaoDeveBloquear()
        {
            // Sem ano não dá para afirmar que é posterior ao corte; bloquear aqui
            // seria punir o imóvel por deficiência da camada.
            var resultado = Avaliar(Sobreposicao(TipoCamada.DesmatamentoConsolidado, ano: null));

            resultado.Status.Should().Be(Status.Liberado);
        }

        [Fact]
        public void Avaliar_AssentamentoAbaixoDoLimiar_NaoDeveAlertar()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.AssentamentoRural, areaHa: 1, percentual: 2));

            resultado.Status.Should().Be(Status.Liberado);
        }

        [Fact]
        public void Avaliar_AssentamentoAcimaDoLimiar_DeveAlertar()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.AssentamentoRural, areaHa: 60, percentual: 6));

            resultado.Status.Should().Be(Status.Alerta);
        }

        [Fact]
        public void Avaliar_CamadaSemRegra_DeveSerIgnorada()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.Bioma, areaHa: 1000, percentual: 100));

            resultado.Status.Should().Be(Status.Liberado, "bioma é contexto, não restrição");
        }

        [Fact]
        public void Avaliar_ComAchados_ParecerDeveCitarEvidenciaEFundamento()
        {
            var resultado = Avaliar(Sobreposicao(TipoCamada.EmbargoAmbiental, areaHa: 12.5, percentual: 1.25));

            resultado.Parecer.Should().Contain("EMB-001");
            resultado.Parecer.Should().Contain("Feição de teste");
            resultado.Parecer.Should().Contain("Origem de teste");
            resultado.Parecer.Should().Contain("12,50");
            resultado.Parecer.Should().Contain("Fundamento");
            resultado.Parecer.Should().Contain(Car);
        }

        [Fact]
        public void Avaliar_MuitasSobreposicoesDaMesmaCamada_ResumoDeveCaberNaColunaDaSolicitacao()
        {
            // Caso real: um imóvel sobre PRODES tocou 11 polígonos da mesma camada
            // e o laudo completo estourou o limite de 2000 caracteres de
            // Solicitacao.Parecer, derrubando a gravação com erro de truncamento.
            var muitas = Enumerable.Range(0, 60)
                .Select(i => Sobreposicao(TipoCamada.DesmatamentoConsolidado, areaHa: 10 + i, ano: 2024,
                                          nome: "PRODES Amazônia 2024"))
                .ToArray();

            var resultado = Avaliar(muitas);

            resultado.Status.Should().Be(Status.Bloqueado);
            resultado.Resumo.Length.Should().BeLessThanOrEqualTo(2000);
            resultado.Achados.Should().HaveCount(60, "cada sobreposição continua sendo evidência");
        }

        [Fact]
        public void Avaliar_SobreposicoesDaMesmaCamada_ParecerDeveAgruparEmVezDeListarUmaAUma()
        {
            var resultado = Avaliar(
                Sobreposicao(TipoCamada.DesmatamentoConsolidado, areaHa: 100, percentual: 10, ano: 2024, nome: "PRODES 2024"),
                Sobreposicao(TipoCamada.DesmatamentoConsolidado, areaHa: 50, percentual: 5, ano: 2024, nome: "PRODES 2024"),
                Sobreposicao(TipoCamada.DesmatamentoConsolidado, areaHa: 25, percentual: 2.5, ano: 2024, nome: "PRODES 2024"));

            // Uma linha por regra+camada, com o total somado — e não três blocos.
            resultado.Parecer.Should().Contain("175,00 ha");
            resultado.Parecer.Should().Contain("17,50%");
            resultado.Parecer.Should().Contain("3 polígono(s)");
            resultado.Parecer.Should().Contain("1 regra(s) acionada(s), 3 sobreposição(ões)");
        }

        [Fact]
        public void Avaliar_SemAchados_ResumoDeveDizerLiberado()
        {
            var resultado = Avaliar();

            resultado.Resumo.Should().Contain("LIBERADO");
            resultado.Resumo.Length.Should().BeLessThanOrEqualTo(2000);
        }

        [Fact]
        public void Satisfeita_TipoDiferente_DeveSerFalso()
        {
            var regra = new RegraAnalise { Tipo = TipoCamada.TerraIndigena, Severidade = Severidade.Bloqueio };

            regra.Satisfeita(Sobreposicao(TipoCamada.UnidadeConservacao)).Should().BeFalse();
        }

        [Fact]
        public void Satisfeita_SemLimiar_QualquerSobreposicaoDispara()
        {
            var regra = new RegraAnalise { Tipo = TipoCamada.EmbargoAmbiental };

            regra.Satisfeita(Sobreposicao(TipoCamada.EmbargoAmbiental, areaHa: 0.01, percentual: 0.001))
                 .Should().BeTrue();
        }

        [Fact]
        public void Satisfeita_ApenasUmDosLimiaresAtingido_DeveDisparar()
        {
            // Área pequena num imóvel pequeno é proporcionalmente grave.
            var regra = new RegraAnalise
            {
                Tipo = TipoCamada.OutroPerimetro,
                AreaMinimaHa = 100,
                PercentualMinimo = 5
            };

            regra.Satisfeita(Sobreposicao(TipoCamada.OutroPerimetro, areaHa: 3, percentual: 30))
                 .Should().BeTrue();

            regra.Satisfeita(Sobreposicao(TipoCamada.OutroPerimetro, areaHa: 150, percentual: 0.5))
                 .Should().BeTrue();

            regra.Satisfeita(Sobreposicao(TipoCamada.OutroPerimetro, areaHa: 3, percentual: 0.5))
                 .Should().BeFalse();
        }
    }
}
