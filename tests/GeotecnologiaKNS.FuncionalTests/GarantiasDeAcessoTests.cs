using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace GeotecnologiaKNS.FuncionalTests
{
    /// <summary>
    /// As garantias que só aparecem com a aplicação de pé: autenticação,
    /// isolamento entre indústrias, antifalsificação e a troca obrigatória da
    /// senha provisória.
    /// </summary>
    public class GarantiasDeAcessoTests : IClassFixture<AplicacaoDeTeste>
    {
        private readonly AplicacaoDeTeste _app;

        public GarantiasDeAcessoTests(AplicacaoDeTeste app) => _app = app;

        [Theory]
        [InlineData("/")]
        [InlineData("/Produtores")]
        [InlineData("/Propriedades")]
        [InlineData("/Solicitacoes")]
        [InlineData("/Industrias")]
        public async Task SemLogin_TelaProtegida_DeveMandarParaOLogin(string caminho)
        {
            var cliente = ClienteAutenticado.Criar(_app);

            var resposta = await cliente.GetAsync(caminho);

            resposta.StatusCode.Should().Be(HttpStatusCode.Found);
            resposta.Destino().Should().Contain("/Identity/Account/Login");
        }

        [Fact]
        public async Task ComLogin_DeveChegarAoPainel()
        {
            var cliente = ClienteAutenticado.Criar(_app);

            var login = await cliente.EntrarAsync(AplicacaoDeTeste.EmailA);
            login.StatusCode.Should().Be(HttpStatusCode.Found);

            var painel = await cliente.GetAsync("/");
            painel.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SenhaErrada_NaoDeveAutenticar()
        {
            var cliente = ClienteAutenticado.Criar(_app);

            var login = await cliente.EntrarAsync(AplicacaoDeTeste.EmailA, "senha-errada");

            // Recusa devolve a própria página com o erro, não um redirecionamento.
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            (await cliente.GetAsync("/")).StatusCode.Should().Be(HttpStatusCode.Found);
        }

        [Fact]
        public async Task DocumentoDeOutraIndustria_NaoDeveSerAlcancavelPeloId()
        {
            // A garantia central do produto: as indústrias clientes são
            // concorrentes entre si, e os endpoints de documento buscam por Id
            // inteiro — bastaria iterar para ler o alheio.
            var cliente = ClienteAutenticado.Criar(_app);
            await cliente.EntrarAsync(AplicacaoDeTeste.EmailA);

            var resposta = await cliente.GetAsync($"/Propriedades/ViewFile/{_app.DocumentoDoTenantB}");

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Upload_SemTokenDeAntifalsificacao_DeveSerRecusado()
        {
            var cliente = ClienteAutenticado.Criar(_app);
            await cliente.EntrarAsync(AplicacaoDeTeste.EmailA);

            var conteudo = new MultipartFormDataContent
            {
                { new StringContent("1"), "vinculoId" },
                { new StringContent("forjado.txt"), "Descricao" },
                { new StringContent("text/plain"), "ContentType" }
            };

            var arquivo = new ByteArrayContent(new byte[] { 1, 2, 3 });
            arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            conteudo.Add(arquivo, "Dados", "forjado.txt");

            var resposta = await cliente.PostAsync("/Produtores/Upload", conteudo);

            resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SenhaProvisoria_DeveBarrarANavegacaoAteATroca()
        {
            var cliente = ClienteAutenticado.Criar(_app);
            await cliente.EntrarAsync(AplicacaoDeTeste.EmailProvisorio);

            foreach (var caminho in new[] { "/", "/Produtores", "/Identity/Account/Manage/Index" })
            {
                var resposta = await cliente.GetAsync(caminho);

                resposta.StatusCode.Should().Be(HttpStatusCode.Found, $"o caminho {caminho} deve desviar");
                resposta.Destino().Should().Contain("TrocarSenha");
            }

            (await cliente.GetAsync("/Identity/Account/TrocarSenha"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SenhaProvisoria_DepoisDaTroca_DeveLiberarANavegacao()
        {
            var cliente = ClienteAutenticado.Criar(_app);
            await cliente.EntrarAsync(AplicacaoDeTeste.EmailProvisorio);

            var token = await cliente.ObterTokenAsync("/Identity/Account/TrocarSenha");

            var troca = await cliente.PostAsync("/Identity/Account/TrocarSenha", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["Input.NovaSenha"] = "OutraSenha@2026",
                    ["Input.Confirmacao"] = "OutraSenha@2026"
                }));

            troca.StatusCode.Should().Be(HttpStatusCode.Found);

            (await cliente.GetAsync("/")).StatusCode.Should().Be(HttpStatusCode.OK);

            // E a tela de troca deixa de insistir.
            (await cliente.GetAsync("/Identity/Account/TrocarSenha"))
                .StatusCode.Should().Be(HttpStatusCode.Found);
        }
    }
}
