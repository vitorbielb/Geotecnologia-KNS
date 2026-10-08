using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// A regra sobre a validade do cadastro do imóvel.
    /// </summary>
    /// <remarks>
    /// Cancelamento de CAR não é embargo, e a diferença não é formalidade.
    /// Embargo é sanção: vem de auto de infração, recai sobre área determinada
    /// e proíbe atividade econômica ali — por isso bloqueia. Cancelamento é
    /// anulação de registro: diz que o cadastro não vale, não que a área está
    /// sob sanção, e pode não haver infração nenhuma.
    ///
    /// O que o cancelamento é, de fato, incomoda de outro jeito: todas as
    /// regras geográficas são calculadas contra o perímetro do CAR. Com o
    /// cadastro anulado, o cruzamento rodou sobre um polígono que o Estado não
    /// reconhece mais como a declaração daquele imóvel.
    ///
    /// A parte que estes testes mais protegem é a separação dos motivos. O
    /// SICAR cancela cadastro por cinco razões, e três são rotina de cartório:
    /// divisa de município que mudou, cadastro duplicado, pedido do próprio
    /// dono. Na base carregada são 551 imóveis nessas três contra 80.900 nas
    /// outras duas. Uma regra única sobre "Cancelado" colocaria redesenho de
    /// divisa municipal no mesmo balde que anulação judicial — e afirmar
    /// restrição onde não há é o mesmo erro que liberar onde há, invertido.
    /// </remarks>
    public class SituacaoDoCarTests
    {
        private const string Car = "GO-5200829-E7A4513DC342419A992C0549E41EC8A9";

        private static IReadOnlyList<TipoCamada> TodosOsTipos =>
            PoliticaAnalise.Padrao().Regras
                .Where(r => r.EhGeografica)
                .Select(r => r.Tipo)
                .Distinct()
                .ToList();

        private static ResultadoAvaliacao Avaliar(string? situacao) =>
            new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(
                    Car, 2203.71, Array.Empty<Sobreposicao>(), DateTime.UtcNow, TodosOsTipos),
                PoliticaAnalise.Padrao(),
                cadastro: new CadastroDoImovel(situacao));

        private static Achado? Achado(ResultadoAvaliacao r) =>
            r.Achados.SingleOrDefault(a => a.CodigoRegra == "CAR-001");

        [Theory]
        [InlineData("Cancelado por decisão administrativa")]
        [InlineData("Cancelado por decisão judicial")]
        public void CancelamentoComConteudoDecisorio_DeveDisparar(string situacao)
        {
            Achado(Avaliar(situacao)).Should().NotBeNull();
        }

        [Theory]
        [InlineData("Cancelado por duplicidade")]
        [InlineData("Cancelado por alteração na base de municípios")]
        [InlineData("Cancelado por solicitação do proprietário/possuidor")]
        public void CancelamentoDeRotina_NaoDeveDisparar(string situacao)
        {
            // Divisa de município que mudou não é restrição ambiental. Tratar
            // como se fosse é afirmar o que não se apurou — o mesmo defeito que
            // liberar sem verificar, só que ao contrário.
            Achado(Avaliar(situacao)).Should().BeNull();
        }

        [Theory]
        [InlineData("Aguardando análise")]
        [InlineData("Analisado sem pendências")]
        [InlineData("Analisado, em conformidade com a Lei nº 12.651/2012")]
        [InlineData("Analisado, aguardando atendimento a notificação")]
        public void CadastroVigente_NaoDeveDisparar(string situacao)
        {
            Achado(Avaliar(situacao)).Should().BeNull();
        }

        [Fact]
        public void Cancelamento_DeveSerAlertaENaoBloqueio()
        {
            // O critério da indústria: bloqueio é para área embargada. O
            // cadastro anulado não é sanção sobre a área, então ele avisa.
            var resultado = Avaliar("Cancelado por decisão administrativa");

            Achado(resultado)!.Severidade.Should().Be(Severidade.Alerta);
            resultado.Status.Should().NotBe(Status.Bloqueado);
        }

        [Fact]
        public void Cancelamento_NaoDeveAparecerComoEmbargo()
        {
            // O ponto da regra separada. Escrever "área embargada" sobre um
            // imóvel sem infração destruiria a credibilidade do laudo pelo lado
            // oposto ao de liberar o que não foi verificado.
            var resultado = Avaliar("Cancelado por decisão administrativa");

            resultado.Achados.Should().NotContain(a => a.CodigoRegra.StartsWith("EMB-"));
            Achado(resultado)!.Descricao.Should().NotContain("embarg");
        }

        [Fact]
        public void OAchado_NaoDeveTerAreaNemPercentual()
        {
            // O problema é o registro inteiro, não um pedaço do imóvel.
            var achado = Achado(Avaliar("Cancelado por decisão judicial"))!;

            achado.AreaSobrepostaHa.Should().Be(0);
            achado.PercentualDoImovel.Should().Be(0);
            achado.Escopo.Should().Be(EscopoDoAchado.Cadastro);
        }

        [Fact]
        public void OAchado_DeveCitarASituacaoComoAOrigemAPublica()
        {
            // Quem lê o laudo precisa saber qual cancelamento foi, para ir
            // atrás no órgão. "Cadastro sem validade" sozinho não leva a lugar
            // nenhum.
            Achado(Avaliar("Cancelado por decisão judicial"))!
                .Rotulo.Should().Be("Cancelado por decisão judicial");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SituacaoNaoInformada_DeveSairComoNaoAvaliada(string? situacao)
        {
            // Não é o mesmo que cadastro válido. Um imóvel cuja situação
            // ninguém conferiu não é um imóvel com cadastro em ordem — o mesmo
            // cuidado que vale para a cadeia indireta.
            var regra = Avaliar(situacao).NaoAvaliadas.Single(r => r.CodigoRegra == "CAR-001");

            regra.Motivo.Should().Be(MotivoNaoAvaliada.SituacaoDoCarDesconhecida);
            regra.Explicacao.Should().Contain("não foi informada");
        }

        [Fact]
        public void SemCadastroInformado_NaoDeveLiberar()
        {
            new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(
                    Car, 1000, Array.Empty<Sobreposicao>(), DateTime.UtcNow, TodosOsTipos),
                PoliticaAnalise.Padrao())
                .NaoAvaliadas.Should().Contain(r => r.CodigoRegra == "CAR-001");
        }

        [Fact]
        public void CadastroVigente_DeveContarComoAvaliada()
        {
            // O contraponto: sem este caso, os demais estariam satisfeitos por
            // uma regra que nunca é dada por cumprida.
            Avaliar("Analisado sem pendências")
                .NaoAvaliadas.Should().NotContain(r => r.CodigoRegra == "CAR-001");
        }

        [Fact]
        public void ARegra_NaoDeveSerGeografica()
        {
            // Ela não examina camada nenhuma; se entrasse no eixo geográfico,
            // sairia como "camada não carregada" e mandaria carregar um arquivo
            // que não existe.
            var regra = PoliticaAnalise.Padrao().Regras.Single(r => r.Codigo == "CAR-001");

            regra.SituacaoDoCar.Should().BeTrue();
            regra.EhGeografica.Should().BeFalse();
            regra.EhPorDocumento.Should().BeFalse();
            regra.CadeiaIndireta.Should().BeFalse();
        }
    }
}
