using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// A checagem que a geografia não alcança.
    /// </summary>
    /// <remarks>
    /// Dois casos justificam a regra: o embargo sem área delimitada — quase
    /// metade dos termos do IBAMA — e o produtor cujo imóvel está limpo mas que
    /// responde por embargo em outra fazenda. Nenhum cruzamento de polígono
    /// encontra qualquer um dos dois.
    /// </remarks>
    public class RestricaoPorDocumentoTests
    {
        private const string Car = "PA-1506807-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        private static IReadOnlyList<TipoCamada> TodosOsTipos =>
            PoliticaAnalise.Padrao().Regras
                .Where(r => !r.EhPorDocumento)
                .Select(r => r.Tipo)
                .Distinct()
                .ToList();

        private static ResultadoCruzamento ImovelLimpo() =>
            new(Car, AreaImovelHa: 1000, Array.Empty<Sobreposicao>(), DateTime.UtcNow, TodosOsTipos);

        private static AchadoPorDocumento Embargo(bool comGeometria = false) =>
            new("00000836230", TipoRestricao.EmbargoAmbiental, "IBAMA — Termos de embargo",
                "Fulano de Tal", "627420", "Maués", "AM", "10/02/2024", comGeometria);

        private static ConsultaPorDocumento Consulta(params AchadoPorDocumento[] achados) =>
            new("00000836230", achados,
                new[] { TipoRestricao.EmbargoAmbiental, TipoRestricao.TrabalhoEscravo });

        [Fact]
        public void Avaliar_ImovelLimpoComProdutorEmbargado_DeveBloquear()
        {
            // O ponto da regra: o perímetro não toca nada, e ainda assim a
            // compra é vedada — a restrição é da pessoa.
            var resultado = new MotorDeRegras().Avaliar(
                ImovelLimpo(), PoliticaAnalise.Padrao(), Consulta(Embargo()));

            resultado.Status.Should().Be(Status.Bloqueado);
            resultado.Achados.Should().ContainSingle(a => a.CodigoRegra == "EMB-002");
        }

        [Fact]
        public void Avaliar_ProdutorEmbargado_LaudoNaoDeveFalarEmArea()
        {
            var resultado = new MotorDeRegras().Avaliar(
                ImovelLimpo(), PoliticaAnalise.Padrao(), Consulta(Embargo()));

            // Escrever "Sobreposição: 0,00 ha (0,00% do imóvel)" faria a
            // restrição parecer irrelevante, quando é o contrário.
            resultado.Parecer.Should().Contain("Independe da localização do imóvel");
            resultado.Parecer.Should().NotContain("Sobreposição: 0,00 ha");
            resultado.Parecer.Should().Contain("627420");
        }

        [Fact]
        public void Avaliar_ProdutorNoCadastroDeEmpregadores_DeveBloquear()
        {
            var trabalhoEscravo = new AchadoPorDocumento(
                "41732445000224", TipoRestricao.TrabalhoEscravo, "MTE — Cadastro de Empregadores",
                "ALTO FORTE FLORESTAS LTDA", "Ação fiscal 2025 — item 36", "CARAÍ", "MG",
                "06/10/2025", false);

            var consulta = new ConsultaPorDocumento(
                "41732445000224",
                new[] { trabalhoEscravo },
                new[] { TipoRestricao.EmbargoAmbiental, TipoRestricao.TrabalhoEscravo });

            var resultado = new MotorDeRegras().Avaliar(
                ImovelLimpo(), PoliticaAnalise.Padrao(), consulta);

            resultado.Status.Should().Be(Status.Bloqueado);
            resultado.Achados.Should().ContainSingle(a => a.CodigoRegra == "TRB-001");
            resultado.Parecer.Should().Contain("ALTO FORTE FLORESTAS LTDA");
        }

        [Fact]
        public void Avaliar_ProdutorLimpo_NaoDeveBloquear()
        {
            var resultado = new MotorDeRegras().Avaliar(
                ImovelLimpo(), PoliticaAnalise.Padrao(), Consulta());

            resultado.Achados.Should().NotContain(a => a.CodigoRegra == "EMB-002");
        }

        [Fact]
        public void Avaliar_SemListaCarregada_DeveMarcarRegraComoNaoAvaliada()
        {
            var semLista = new ConsultaPorDocumento(
                "00000836230", Array.Empty<AchadoPorDocumento>(), Array.Empty<TipoRestricao>());

            var resultado = new MotorDeRegras().Avaliar(
                ImovelLimpo(), PoliticaAnalise.Padrao(), semLista);

            resultado.NaoAvaliadas.Should().Contain(r => r.CodigoRegra == "EMB-002");
            resultado.Status.Should().NotBe(Status.Liberado);
        }

        [Fact]
        public void Avaliar_ProdutorSemDocumento_DeveMarcarRegraComoNaoAvaliada()
        {
            // Produtor cadastrado sem CPF não foi verificado, e dizer que
            // passou seria afirmar o que não se apurou.
            var semDocumento = new ConsultaPorDocumento(
                null, Array.Empty<AchadoPorDocumento>(), new[] { TipoRestricao.EmbargoAmbiental });

            var resultado = new MotorDeRegras().Avaliar(
                ImovelLimpo(), PoliticaAnalise.Padrao(), semDocumento);

            resultado.NaoAvaliadas.Should().Contain(r => r.CodigoRegra == "EMB-002");
        }

        [Theory]
        [InlineData("000.008.362-30", "00000836230")]
        [InlineData("00000836230", "00000836230")]
        [InlineData("11.222.333/0001-81", "11222333000181")]
        public void Normalizar_DocumentoValido_DeveDeixarSoDigitos(string entrada, string esperado)
        {
            RestricaoDocumentoService.Normalizar(entrada).Should().Be(esperado);
        }

        [Theory]
        [InlineData("00000000000")]
        [InlineData("11111111111")]
        [InlineData("00000000000000")]
        public void Normalizar_DocumentoDePreenchimento_DeveSerRecusado(string entrada)
        {
            // A base do IBAMA lavra termos com 00000000000 quando o autuado não
            // é identificado. Aceitar isso bloquearia qualquer produtor
            // cadastrado com documento de preenchimento.
            RestricaoDocumentoService.Normalizar(entrada).Should().BeNull();
        }

        [Theory]
        [InlineData("123")]
        [InlineData("")]
        [InlineData(null)]
        public void Normalizar_DocumentoInvalido_DeveSerRecusado(string? entrada)
        {
            RestricaoDocumentoService.Normalizar(entrada).Should().BeNull();
        }
    }
}
