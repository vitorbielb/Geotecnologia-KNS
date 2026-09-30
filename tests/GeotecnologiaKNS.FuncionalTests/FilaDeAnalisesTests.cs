using System.Net;
using FluentAssertions;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GeotecnologiaKNS.FuncionalTests
{
    /// <summary>
    /// A análise sai de dentro da requisição.
    /// </summary>
    /// <remarks>
    /// Antes, abrir uma solicitação cruzava o perímetro contra as camadas ali
    /// mesmo, e o usuário esperava. Com a base nacional e as oito camadas isso
    /// passaria do tempo limite, e ele receberia erro de tela por um trabalho
    /// que na verdade rodou. O que este teste trava é a separação: a requisição
    /// volta com a análise ainda por fazer.
    /// </remarks>
    public class FilaDeAnalisesTests : IClassFixture<AplicacaoDeTeste>
    {
        private readonly AplicacaoDeTeste _app;

        public FilaDeAnalisesTests(AplicacaoDeTeste app) => _app = app;

        [Fact]
        public async Task AbrirSolicitacao_DeveEnfileirarSemExecutar()
        {
            var cliente = ClienteAutenticado.Criar(_app);
            await cliente.EntrarAsync(AplicacaoDeTeste.EmailA);

            var token = await cliente.ObterTokenAsync("/Solicitacoes/Create");

            var resposta = await cliente.PostAsync("/Solicitacoes/Create", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["PropriedadeId"] = _app.ImovelDoTenantA.ToString(),
                    ["Solicitante"] = "Teste de fila",
                    ["Observacao"] = "Deve voltar sem analisar"
                }));

            resposta.StatusCode.Should().Be(HttpStatusCode.Found);

            using var escopo = _app.Services.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var solicitacaoId = IdDoDestino(resposta);

            var analise = await contexto.AnalisesAutomaticas
                .FirstOrDefaultAsync(x => x.SolicitacaoId == solicitacaoId);

            analise.Should().NotBeNull();
            analise!.Situacao.Should().Be(SituacaoAnalise.Pendente);
            analise.ConcluidaEm.Should().BeNull();

            // Situação Pendente sozinha não prova nada: uma análise processada
            // que falhou volta a Pendente para nova tentativa, e neste ambiente
            // não há PostGIS, então processar sempre falharia. O que distingue
            // "só enfileirada" de "processada e falhou" é o que a etapa de
            // processamento preenche antes de tentar o cruzamento.
            var background = string.Join(", ",
                _app.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                    .Select(s => s.GetType().Name));

            analise.PoliticaAplicada.Should().BeNull(
                "o retrato da política é tirado ao processar. background=[" + background + "]");
            analise.Politica.Should().BeEmpty();
            analise.Erro.Should().BeNull("nenhuma tentativa de cruzamento deve ter ocorrido");
        }

        [Fact]
        public async Task SolicitacaoRecemAberta_NaoDeveNascerLiberada()
        {
            // Enquanto a análise não roda, a solicitação não pode estar
            // aprovada: liberar por omissão é o defeito que a fila não pode
            // reintroduzir.
            var cliente = ClienteAutenticado.Criar(_app);
            await cliente.EntrarAsync(AplicacaoDeTeste.EmailA);

            var token = await cliente.ObterTokenAsync("/Solicitacoes/Create");

            await cliente.PostAsync("/Solicitacoes/Create", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["PropriedadeId"] = _app.ImovelDoTenantA.ToString(),
                    ["Solicitante"] = "Teste de fila",
                    ["Observacao"] = "Status inicial"
                }));

            using var escopo = _app.Services.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var solicitacao = await contexto.Solicitacao
                .OrderByDescending(x => x.Id)
                .FirstAsync();

            solicitacao.Status.Should().Be(Status.Solicitado);
        }

        /// <summary>
        /// Id da solicitação recém-criada, lido do redirecionamento.
        /// </summary>
        /// <remarks>
        /// Olhar "a última análise do banco" tornaria o teste dependente da
        /// ordem de execução: os dois testes desta classe dividem o mesmo banco
        /// e ambos abrem solicitação.
        /// </remarks>
        private static int IdDoDestino(HttpResponseMessage resposta)
        {
            var destino = resposta.Headers.Location?.ToString()
                ?? throw new InvalidOperationException("A criação não redirecionou.");

            var ultimo = destino[(destino.LastIndexOf('/') + 1)..];

            return int.TryParse(ultimo, out var id)
                ? id
                : throw new InvalidOperationException($"Destino inesperado: {destino}");
        }
    }
}
