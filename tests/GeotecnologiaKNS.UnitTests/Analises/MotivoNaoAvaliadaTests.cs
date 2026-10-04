using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// Por que cada regra ficou sem ser aplicada, e o que isso faz com o veredito.
    /// </summary>
    /// <remarks>
    /// Nasceu de duas coisas erradas na tela de análise, vistas por quem estava
    /// testando:
    ///
    /// A primeira: "não puderam ser aplicadas porque nenhuma camada do tipo que
    /// elas examinam estava carregada", listando a IND-001 logo abaixo. A
    /// IND-001 não examina camada alguma — ela fica sem avaliar quando ninguém
    /// declarou a cadeia. Mandar carregar um arquivo é a instrução errada.
    ///
    /// A segunda: "O resultado não é uma liberação" impresso embaixo de um selo
    /// verde escrito LIBERADO. A contradição apareceu quando a IND-001 passou a
    /// ser informativa por padrão, e regra informativa não avaliada não derruba
    /// o veredito.
    /// </remarks>
    public class MotivoNaoAvaliadaTests
    {
        private const string Car = "MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        private static IReadOnlyList<TipoCamada> TodosOsTipos =>
            PoliticaAnalise.Padrao().Regras
                .Where(r => r.EhGeografica)
                .Select(r => r.Tipo)
                .Distinct()
                .ToList();

        private static ResultadoAvaliacao Avaliar(
            ConsultaPorDocumento? documento,
            CadeiaIndireta? cadeia,
            IReadOnlyList<TipoCamada>? tipos = null) =>
            new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(
                    Car, 1000, Array.Empty<Sobreposicao>(), DateTime.UtcNow, tipos ?? TodosOsTipos),
                PoliticaAnalise.Padrao(),
                documento,
                cadeia);

        private static ConsultaPorDocumento ProdutorLimpo =>
            new("00000836230", Array.Empty<AchadoPorDocumento>(),
                new[] { TipoRestricao.EmbargoAmbiental, TipoRestricao.TrabalhoEscravo });

        [Fact]
        public void CadeiaNaoDeclarada_NaoDeveMandarCarregarCamada()
        {
            var resultado = Avaliar(ProdutorLimpo, cadeia: null);

            var regra = resultado.NaoAvaliadas.Single(r => r.CodigoRegra == "IND-001");

            regra.Motivo.Should().Be(MotivoNaoAvaliada.CadeiaNaoInformada);
            regra.Explicacao.Should().Contain("fornecedor indireto declarado");
            regra.Explicacao.Should().NotContain("camada");
        }

        [Fact]
        public void ProdutorSemDocumento_DeveDizerQueFaltaODocumento()
        {
            var semDocumento = new ConsultaPorDocumento(
                null, Array.Empty<AchadoPorDocumento>(),
                new[] { TipoRestricao.EmbargoAmbiental, TipoRestricao.TrabalhoEscravo });

            var resultado = Avaliar(semDocumento, cadeia: null);

            resultado.NaoAvaliadas.Single(r => r.CodigoRegra == "EMB-002")
                .Motivo.Should().Be(MotivoNaoAvaliada.ProdutorSemDocumento);
        }

        [Fact]
        public void ListaRestritivaAusente_DeveSerDistinguidaDeProdutorSemDocumento()
        {
            // São duas faltas diferentes e se resolvem em lugares diferentes:
            // uma é carregar a lista, a outra é completar o cadastro do produtor.
            var semLista = new ConsultaPorDocumento(
                "00000836230", Array.Empty<AchadoPorDocumento>(), Array.Empty<TipoRestricao>());

            var resultado = Avaliar(semLista, cadeia: null);

            resultado.NaoAvaliadas.Single(r => r.CodigoRegra == "EMB-002")
                .Motivo.Should().Be(MotivoNaoAvaliada.ListaRestritivaAusente);
        }

        [Fact]
        public void CamadaAusente_DeveContinuarApontandoACamada()
        {
            var resultado = Avaliar(
                ProdutorLimpo, cadeia: null, tipos: new[] { TipoCamada.EmbargoAmbiental });

            resultado.NaoAvaliadas.Single(r => r.CodigoRegra == "TI-001")
                .Motivo.Should().Be(MotivoNaoAvaliada.CamadaAusente);
        }

        [Fact]
        public void LacunaInformativa_NaoDeveAlterarOVeredito()
        {
            // A contradição que apareceu na tela: só a IND-001 sem avaliar, que
            // é informativa no padrão, e ainda assim o texto dizia que o
            // resultado não era uma liberação.
            var resultado = Avaliar(ProdutorLimpo, cadeia: null);

            resultado.CoberturaCompleta.Should().BeFalse();
            resultado.Status.Should().Be(Status.Liberado);
            resultado.NaoAvaliadas.Should().OnlyContain(r => !r.AlteraOVeredito);
        }

        [Fact]
        public void LacunaQueDecide_DeveDerrubarOVeredito()
        {
            var resultado = Avaliar(
                ProdutorLimpo, cadeia: null, tipos: new[] { TipoCamada.EmbargoAmbiental });

            resultado.Status.Should().NotBe(Status.Liberado);
            resultado.NaoAvaliadas.Should().Contain(r => r.AlteraOVeredito);
        }

        [Fact]
        public void OLaudo_DeveDizerOMotivoDeCadaFalta()
        {
            var resultado = Avaliar(
                ProdutorLimpo, cadeia: null, tipos: new[] { TipoCamada.EmbargoAmbiental });

            resultado.Parecer.Should().Contain("nenhum fornecedor indireto declarado");
            resultado.Parecer.Should().Contain("nenhuma camada do tipo examinado");
        }
    }
}
