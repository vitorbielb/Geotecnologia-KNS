using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// A regra OUT-001, que a indústria alimenta com os próprios perímetros.
    /// </summary>
    /// <remarks>
    /// Era a única das dez regras do protocolo sem base: existia na política,
    /// nunca tinha o que avaliar, e saía como "não avaliada" em todo laudo
    /// emitido. Agora ela só é dada por avaliada quando a indústria tem de fato
    /// um perímetro carregado — que é a diferença entre "olhei e não achei" e
    /// "não tinha onde olhar".
    /// </remarks>
    public class PerimetroProprioTests
    {
        private const string Car = "MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        private static IReadOnlyList<TipoCamada> TiposPublicos =>
            PoliticaAnalise.Padrao().Regras
                .Where(r => r.EhGeografica && r.Tipo != TipoCamada.OutroPerimetro)
                .Select(r => r.Tipo)
                .Distinct()
                .ToList();

        private static ConsultaPorDocumento ProdutorLimpo =>
            new("00000836230", Array.Empty<AchadoPorDocumento>(),
                new[] { TipoRestricao.EmbargoAmbiental, TipoRestricao.TrabalhoEscravo });

        private static Sobreposicao PerimetroProprio(double areaHa, double percentual) =>
            new("perimetro-1-abc", "Áreas de conflito 2026", TipoCamada.OutroPerimetro,
                "Perímetro declarado pela indústria", null,
                "Gleba Sorriso", null, areaHa, percentual);

        /// <summary>Cadeia declarada e limpa, para a IND-001 não contaminar a medida.</summary>
        private static CadeiaIndireta CadeiaLimpa =>
            new(1, new[]
            {
                new FornecedorIndiretoAvaliado(
                    "MT-1", "Fornecedor limpo", Verificado: true, Array.Empty<string>(), null)
            });

        private static ResultadoAvaliacao Avaliar(
            IReadOnlyList<TipoCamada> verificados, params Sobreposicao[] sobreposicoes) =>
            new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(Car, 1000, sobreposicoes, DateTime.UtcNow, verificados),
                PoliticaAnalise.Padrao(),
                ProdutorLimpo,
                CadeiaLimpa);

        [Fact]
        public void SemPerimetroCarregado_OUT001_DeveSairComoNaoAvaliada()
        {
            // Esta era a situação permanente antes da tela de perímetros.
            var resultado = Avaliar(TiposPublicos);

            resultado.NaoAvaliadas.Should().Contain(r => r.CodigoRegra == "OUT-001");
            resultado.CoberturaCompleta.Should().BeFalse();
            resultado.Status.Should().NotBe(Status.Liberado);
        }

        [Fact]
        public void ComPerimetroCarregadoESemSobreposicao_OUT001_DeveSairAvaliada()
        {
            var comPerimetro = TiposPublicos.Append(TipoCamada.OutroPerimetro).ToList();

            var resultado = Avaliar(comPerimetro);

            resultado.NaoAvaliadas.Should().NotContain(r => r.CodigoRegra == "OUT-001");
            resultado.CoberturaCompleta.Should().BeTrue();
            resultado.Status.Should().Be(Status.Liberado);
        }

        [Fact]
        public void ComSobreposicaoNoPerimetroProprio_DeveAlertar()
        {
            var comPerimetro = TiposPublicos.Append(TipoCamada.OutroPerimetro).ToList();

            var resultado = Avaliar(comPerimetro, PerimetroProprio(areaHa: 120, percentual: 12));

            resultado.Achados.Should().ContainSingle(a => a.CodigoRegra == "OUT-001");
            resultado.Status.Should().Be(Status.Alerta);
        }

        [Fact]
        public void OLaudo_DeveNomearOPerimetroEaOrigemDeclarada()
        {
            // Quem lê o laudo precisa distinguir o que veio de base pública do
            // que a própria indústria declarou: a primeira se contesta com o
            // órgão, a segunda se resolve internamente.
            var comPerimetro = TiposPublicos.Append(TipoCamada.OutroPerimetro).ToList();

            var resultado = Avaliar(comPerimetro, PerimetroProprio(areaHa: 120, percentual: 12));

            resultado.Parecer.Should().Contain("Áreas de conflito 2026");
            resultado.Parecer.Should().Contain("Perímetro declarado pela indústria");
        }
    }
}
