using FluentAssertions;
using GeotecnologiaKNS.Laudos;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.UnitTests.Laudos
{
    /// <summary>
    /// O aviso de que a base mudou depois que a análise rodou.
    /// </summary>
    /// <remarks>
    /// Um laudo não fica errado por envelhecer: ele descreve o que havia na
    /// data em que foi executado e continua verdadeiro sobre aquela data. O que
    /// muda é que deixa de descrever o presente.
    ///
    /// Sem este aviso, a única pista no documento é cruzar a data da execução
    /// com a data de carga de cada base listada no rodapé — ninguém faz isso, e
    /// a conclusão silenciosa de quem lê é a errada: que nada mudou.
    ///
    /// O caso que obrigou a existir: as camadas de desmatamento foram
    /// corrigidas, e das cinco análises que diziam LIBERADO as cinco passaram a
    /// apontar achados. Os laudos emitidos a partir delas estavam com alguém.
    /// </remarks>
    public class BasesMudaramDepoisTests
    {
        private static readonly DateTime Execucao = new(2026, 10, 6, 2, 14, 0);

        private static DadosDoLaudo Dados(DateTime? basesEm, bool temConclusao = true) =>
            new()
            {
                Analise = new AnaliseAutomatica
                {
                    Id = 27,
                    TenantId = 1,
                    SolicitacaoId = 23,
                    CodigoCar = "GO-5200829-E7A4513DC342419A992C0549E41EC8A9",
                    Politica = "Protocolo padrão",
                    Situacao = SituacaoAnalise.Concluida,
                    IniciadaEm = Execucao.AddMinutes(-4),
                    ConcluidaEm = temConclusao ? Execucao : null
                },
                Solicitacao = new Solicitacao { Id = 23, Solicitante = "Originação" },
                Propriedade = new Propriedade
                {
                    CodigoCar = "GO-5200829-E7A4513DC342419A992C0549E41EC8A9",
                    NomePropriedade = "Fazenda de teste"
                },
                CodigoDeConferencia = "A1B2-C3D4-E5F6",
                BasesAtualizadasEm = basesEm
            };

        [Fact]
        public void CargaPosteriorAExecucao_DeveAvisar()
        {
            Dados(Execucao.AddDays(5)).BasesMudaramDepois.Should().BeTrue();
        }

        [Fact]
        public void CargaAnteriorAExecucao_NaoDeveAvisar()
        {
            // O caso normal: a análise rodou contra a carga que estava no ar.
            Dados(Execucao.AddDays(-2)).BasesMudaramDepois.Should().BeFalse();
        }

        [Fact]
        public void CargaNoMesmoInstante_NaoDeveAvisar()
        {
            // Igual não é posterior. Avisar aqui encheria de ressalva todo
            // laudo emitido logo depois de uma recarga, e ressalva que aparece
            // sempre deixa de ser lida.
            Dados(Execucao).BasesMudaramDepois.Should().BeFalse();
        }

        [Fact]
        public void SemDataDeCarga_NaoDeveAvisar()
        {
            // Base geoespacial fora do ar na emissão, ou imóvel sem camada que
            // o alcance. Não saber não é o mesmo que saber que mudou.
            Dados(null).BasesMudaramDepois.Should().BeFalse();
        }

        [Fact]
        public void AnaliseSemDataDeConclusao_NaoDeveAvisar()
        {
            // Sem o outro lado da comparação não há o que comparar.
            Dados(Execucao.AddDays(5), temConclusao: false).BasesMudaramDepois.Should().BeFalse();
        }

        [Fact]
        public void DiferencaDeMinutos_DeveAvisar()
        {
            // Sem piso de tolerância de propósito: a recarga é o evento, e o
            // que ela traz de novo pode ser exatamente o embargo que faltava.
            // Em nove dias a base do IBAMA ganhou 111 polígonos.
            Dados(Execucao.AddMinutes(1)).BasesMudaramDepois.Should().BeTrue();
        }
    }
}
