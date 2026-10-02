using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// A regra IND-001, que olha quem vendeu para quem está vendendo.
    /// </summary>
    /// <remarks>
    /// É o elo que falta na maior parte do monitoramento de cadeia de carne: a
    /// fazenda que vende o boi gordo ao frigorífico comprou o bezerro de outra,
    /// e é na outra que o desmatamento aconteceu. Um laudo que olhe só o
    /// fornecedor direto sai limpo sobre uma cadeia que não é.
    ///
    /// O sistema só conhece os fornecedores declarados, e isso tem de aparecer
    /// no laudo com todas as letras — um documento de defesa com base falsa é
    /// pior que nenhum.
    /// </remarks>
    public class CadeiaIndiretaTests
    {
        private const string Car = "MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        private static IReadOnlyList<TipoCamada> TodosOsTipos =>
            PoliticaAnalise.Padrao().Regras
                .Where(r => r.EhGeografica)
                .Select(r => r.Tipo)
                .Distinct()
                .ToList();

        private static ConsultaPorDocumento ProdutorLimpo =>
            new("00000836230", Array.Empty<AchadoPorDocumento>(),
                new[] { TipoRestricao.EmbargoAmbiental, TipoRestricao.TrabalhoEscravo });

        private static ResultadoAvaliacao Avaliar(
            CadeiaIndireta? cadeia, PoliticaAnalise? politica = null) =>
            new MotorDeRegras().Avaliar(
                new ResultadoCruzamento(Car, 1000, Array.Empty<Sobreposicao>(), DateTime.UtcNow, TodosOsTipos),
                politica ?? PoliticaAnalise.Padrao(),
                ProdutorLimpo,
                cadeia);

        /// <summary>
        /// Política de uma indústria que leva a cadeia indireta a sério.
        /// </summary>
        /// <remarks>
        /// O padrão deixa IND-001 como informativa, senão todo laudo sairia como
        /// alerta no dia em que a regra entrasse — quase nenhuma indústria tem a
        /// cadeia mapeada hoje. Quem sobe o corte muda o comportamento, e é esse
        /// caminho que estes testes cobrem.
        /// </remarks>
        private static PoliticaAnalise ComCadeiaExigida()
        {
            var politica = PoliticaAnalise.Padrao();

            politica.Regras.Single(r => r.Codigo == "IND-001").Severidade = Severidade.Alerta;

            return politica;
        }

        private static FornecedorIndiretoAvaliado Limpo(string car) =>
            new(car, "Fulano", Verificado: true, Array.Empty<string>(), null);

        private static FornecedorIndiretoAvaliado ComEmbargo(string car) =>
            new(car, "Beltrano", Verificado: true,
                new[] { "IBAMA — Termos de embargo: TAD 627420" }, null);

        [Fact]
        public void CadeiaNaoInformada_IND001_DeveSairComoNaoAvaliada()
        {
            // O ponto central: não declarar não é o mesmo que estar limpo. A
            // fazenda que vende boi gordo quase sempre comprou bezerro de
            // alguém, e dar o laudo por completo sem saber de quem é afirmar o
            // que não se apurou.
            var resultado = Avaliar(new CadeiaIndireta(0, Array.Empty<FornecedorIndiretoAvaliado>()));

            resultado.NaoAvaliadas.Should().Contain(r => r.CodigoRegra == "IND-001");
            resultado.CoberturaCompleta.Should().BeFalse();
        }

        [Fact]
        public void CadeiaNaoInformada_NoPadrao_NaoDeveDerrubarOVeredito()
        {
            // IND-001 é informativa no padrão, e regra que só informaria não
            // pode, quando não avaliada, decidir mais do que decidiria se
            // tivesse disparado.
            var resultado = Avaliar(new CadeiaIndireta(0, Array.Empty<FornecedorIndiretoAvaliado>()));

            resultado.Status.Should().Be(Status.Liberado);
            resultado.Parecer.Should().Contain("não foi verificada");
        }

        [Fact]
        public void CadeiaNaoInformada_ComRegraExigida_NaoDeveLiberar()
        {
            // A indústria que subiu o corte passa a ter o laudo rebaixado
            // enquanto a cadeia não for declarada — que é exatamente o que ela
            // pediu ao subir.
            var resultado = Avaliar(
                new CadeiaIndireta(0, Array.Empty<FornecedorIndiretoAvaliado>()), ComCadeiaExigida());

            resultado.Status.Should().NotBe(Status.Liberado);
        }

        [Fact]
        public void SemCadeiaNenhuma_DeveSeComportarComoNaoInformada()
        {
            var resultado = Avaliar(null);

            resultado.NaoAvaliadas.Should().Contain(r => r.CodigoRegra == "IND-001");
        }

        [Fact]
        public void CadeiaInformadaELimpa_DeveLiberar()
        {
            var cadeia = new CadeiaIndireta(2, new[] { Limpo("MT-1"), Limpo("MT-2") });

            var resultado = Avaliar(cadeia);

            resultado.NaoAvaliadas.Should().NotContain(r => r.CodigoRegra == "IND-001");
            resultado.CoberturaCompleta.Should().BeTrue();
            resultado.Status.Should().Be(Status.Liberado);
        }

        [Fact]
        public void FornecedorIndiretoComRestricao_DeveAparecerNoLaudo()
        {
            // O imóvel analisado não tem sobreposição nenhuma; o achado é todo
            // da cadeia. No padrão ele é registrado sem mudar o veredito.
            var cadeia = new CadeiaIndireta(2, new[] { Limpo("MT-1"), ComEmbargo("MT-2") });

            var resultado = Avaliar(cadeia);

            resultado.Achados.Should().ContainSingle(a => a.CodigoRegra == "IND-001");
            resultado.Status.Should().Be(Status.Liberado);
            resultado.Parecer.Should().Contain("MT-2");
        }

        [Fact]
        public void FornecedorIndiretoComRestricao_ComRegraExigida_DeveAlertar()
        {
            var cadeia = new CadeiaIndireta(2, new[] { Limpo("MT-1"), ComEmbargo("MT-2") });

            var resultado = Avaliar(cadeia, ComCadeiaExigida());

            resultado.Achados.Should().ContainSingle(a => a.CodigoRegra == "IND-001");
            resultado.Status.Should().Be(Status.Alerta);
        }

        [Fact]
        public void UmAchadoPorFornecedor_ParaQuemLeSaberOndeIrAtras()
        {
            var cadeia = new CadeiaIndireta(3, new[]
            {
                Limpo("MT-1"), ComEmbargo("MT-2"), ComEmbargo("MT-3")
            });

            var resultado = Avaliar(cadeia);

            resultado.Achados.Where(a => a.CodigoRegra == "IND-001").Should().HaveCount(2);
            resultado.Parecer.Should().Contain("MT-2");
            resultado.Parecer.Should().Contain("MT-3");
        }

        [Fact]
        public void OLaudo_DeveDeclararQueSoAlcancaOQueFoiInformado()
        {
            // Sem esta frase, "cadeia indireta verificada" viraria documento de
            // defesa com base falsa — e é num questionamento do Ministério
            // Público que isso apareceria.
            var resultado = Avaliar(new CadeiaIndireta(1, new[] { Limpo("MT-1") }));

            resultado.Parecer.Should().Contain("CADEIA DE FORNECIMENTO INDIRETO");
            resultado.Parecer.Should().Contain("apenas os fornecedores informados");
        }

        [Fact]
        public void OLaudo_SemCadeia_DeveDizerQueNaoFoiVerificada()
        {
            var resultado = Avaliar(null);

            resultado.Parecer.Should().Contain("não foi verificada");
            resultado.Parecer.Should().Contain("o que não significa que não exista");
        }

        [Fact]
        public void FornecedorNaoVerificado_NaoDeveContarComoLimpo()
        {
            // Imóvel fora da base do CAR é o caso comum na ponta da cadeia.
            var foraDaBase = new FornecedorIndiretoAvaliado(
                "PA-9", "Sicrano", Verificado: false, Array.Empty<string>(),
                "Não foi possível cruzar o perímetro: imóvel não está na base do CAR.");

            var resultado = Avaliar(new CadeiaIndireta(1, new[] { foraDaBase }));

            resultado.Parecer.Should().Contain("NÃO VERIFICADO");
            resultado.Parecer.Should().Contain("PA-9");
        }

        [Fact]
        public void OLaudo_NaoDeveFalarEmAreaNoAchadoDaCadeia()
        {
            // A restrição é de outro imóvel; exibir área daria a entender que é
            // deste.
            var resultado = Avaliar(new CadeiaIndireta(1, new[] { ComEmbargo("MT-2") }));

            resultado.Parecer.Should().Contain("O achado é na cadeia, não no imóvel analisado");
            resultado.Parecer.Should().NotContain("Sobreposição: 0,00 ha");
        }
    }
}
