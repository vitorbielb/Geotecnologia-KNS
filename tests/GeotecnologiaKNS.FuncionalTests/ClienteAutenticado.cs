using System.Net;
using System.Text.RegularExpressions;

namespace GeotecnologiaKNS.FuncionalTests
{
    /// <summary>
    /// Cliente HTTP que sabe fazer login e carregar o token de antifalsificação.
    /// </summary>
    /// <remarks>
    /// Os testes daqui atravessam a aplicação inteira — roteamento, filtros,
    /// middleware, Identity, banco. É essa passagem que os testes unitários não
    /// cobrem e que este projeto, até agora vazio, deveria cobrir.
    /// </remarks>
    public static class ClienteAutenticado
    {
        private static readonly Regex Token = new(
            @"name=""__RequestVerificationToken""[^>]*value=""(?<valor>[^""]+)""",
            RegexOptions.Compiled);

        public static HttpClient Criar(AplicacaoDeTeste app) =>
            app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        public static async Task<HttpResponseMessage> EntrarAsync(
            this HttpClient cliente, string email, string senha = AplicacaoDeTeste.SenhaPadrao)
        {
            var pagina = await cliente.GetStringAsync("/Identity/Account/Login");

            return await cliente.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = ExtrairToken(pagina),
                    ["Input.Email"] = email,
                    ["Input.Password"] = senha,
                    ["Input.RememberMe"] = "false"
                }));
        }

        /// <summary>Token de antifalsificação da página informada.</summary>
        public static async Task<string> ObterTokenAsync(this HttpClient cliente, string caminho) =>
            ExtrairToken(await cliente.GetStringAsync(caminho));

        public static string ExtrairToken(string html)
        {
            var achado = Token.Match(html);

            if (!achado.Success)
            {
                throw new InvalidOperationException("A página não trouxe token de antifalsificação.");
            }

            return achado.Groups["valor"].Value;
        }

        public static string? Destino(this HttpResponseMessage resposta) =>
            resposta.StatusCode is HttpStatusCode.Found or HttpStatusCode.Redirect
                ? resposta.Headers.Location?.ToString()
                : null;
    }
}
