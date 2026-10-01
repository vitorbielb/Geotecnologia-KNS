using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// A conferência que decide se uma recarga pode substituir o que está no ar.
    /// </summary>
    /// <remarks>
    /// O caso que justifica a existência dela: o IBAMA publica um CSV truncado
    /// numa madrugada qualquer. Sem conferência, cinquenta e sete mil embargos
    /// bons seriam apagados e duzentos gravados no lugar — e a análise passaria
    /// a liberar o que deveria bloquear, dizendo "nenhuma sobreposição" com a
    /// mesma confiança de sempre. Ninguém perceberia até alguém contestar um
    /// laudo.
    /// </remarks>
    public class GuardaDeCargaTests
    {
        [Fact]
        public void Avaliar_CargaVazia_DeveRecusar()
        {
            // Zero é sempre suspeito: camada de referência não deixa de existir.
            GuardaDeCarga.Avaliar(antes: 57_843, depois: 0)
                .Should().Be(MotivoDaRecusa.Vazia);
        }

        [Fact]
        public void Avaliar_PrimeiraCargaVazia_DeveRecusar()
        {
            // Mesmo sem ter o que preservar: publicar uma camada vazia a
            // marcaria como verificada, e a regra correspondente passaria a se
            // declarar avaliada sem nada contra o que avaliar.
            GuardaDeCarga.Avaliar(antes: 0, depois: 0)
                .Should().Be(MotivoDaRecusa.Vazia);
        }

        [Fact]
        public void Avaliar_ArquivoTruncado_DeveRecusar()
        {
            GuardaDeCarga.Avaliar(antes: 57_843, depois: 200)
                .Should().Be(MotivoDaRecusa.EncolheuDemais);
        }

        [Fact]
        public void Avaliar_PrimeiraCarga_DeveAceitar()
        {
            // Não há com o que comparar, e recusar impediria qualquer camada de
            // entrar pela primeira vez.
            GuardaDeCarga.Avaliar(antes: 0, depois: 495)
                .Should().Be(MotivoDaRecusa.Nenhum);
        }

        [Fact]
        public void Avaliar_ReducaoPlausivel_DeveAceitar()
        {
            // Embargos revogados e unidades de conservação extintas existem, e
            // são dezenas. A guarda não pode confundir isso com arquivo ruim:
            // recusar carga boa congelaria a base no mesmo lugar.
            GuardaDeCarga.Avaliar(antes: 57_843, depois: 57_500)
                .Should().Be(MotivoDaRecusa.Nenhum);
        }

        [Fact]
        public void Avaliar_CrescimentoGrande_DeveAceitar()
        {
            // A guarda olha só para perda. Camada que dobra pode ser mudança de
            // escopo na origem — e aceitar a mais nunca libera quem deveria ser
            // bloqueado.
            GuardaDeCarga.Avaliar(antes: 1_000, depois: 100_000)
                .Should().Be(MotivoDaRecusa.Nenhum);
        }

        [Theory]
        [InlineData(1000, 499, MotivoDaRecusa.EncolheuDemais)]
        [InlineData(1000, 500, MotivoDaRecusa.Nenhum)]
        public void Avaliar_NaFronteira_DeveSeguirAMetade(int antes, int depois, MotivoDaRecusa esperado)
        {
            GuardaDeCarga.Avaliar(antes, depois).Should().Be(esperado);
        }

        [Fact]
        public void Explicacao_Recusa_DeveDizerOQueAconteceuComABaseAtual()
        {
            // Quem lê isso num registro de madrugada precisa saber, em uma
            // linha, que o sistema não ficou sem camada.
            var resultado = new ResultadoDaTroca(
                false, MotivoDaRecusa.EncolheuDemais, Antes: 57_843, Depois: 200);

            resultado.Explicacao.Should().Contain("57.843");
            resultado.Explicacao.Should().Contain("200");
            resultado.Explicacao.Should().Contain("anterior foi mantida");
        }

        [Fact]
        public void Explicacao_ListaRestritiva_DeveFalarEmRegistros()
        {
            // A mesma guarda serve às camadas e às listas por CPF/CNPJ; dizer
            // "feições" numa lista de pessoas confundiria quem opera.
            var resultado = new ResultadoDaTroca(
                false, MotivoDaRecusa.Vazia, Antes: 100_691, Depois: 0, Unidade: "registros");

            resultado.Explicacao.Should().Contain("registros");
            resultado.Explicacao.Should().NotContain("feições");
        }
    }
}
