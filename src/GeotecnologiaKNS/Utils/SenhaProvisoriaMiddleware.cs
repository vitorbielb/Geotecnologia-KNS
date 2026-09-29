using System.Security.Claims;

namespace GeotecnologiaKNS.Utils;

/// <summary>
/// Enquanto a senha for a provisória, a única página alcançável é a de troca.
/// </summary>
/// <remarks>
/// Feito como middleware, e não como filtro de action, porque a troca precisa
/// valer também para as páginas Razor da área Identity — um filtro de MVC não
/// roda nelas, e bastaria digitar /Identity/Account/Manage para escapar.
///
/// O desvio é para a página de troca, e não um aviso dispensável: senha
/// combinada por fora é conhecida por duas pessoas, e enquanto ela valer o log
/// de acesso não identifica quem de fato entrou.
/// </remarks>
public class SenhaProvisoriaMiddleware
{
    private const string PaginaDeTroca = "/Identity/Account/TrocarSenha";

    /// <summary>
    /// Caminhos que continuam liberados: a própria troca, a saída e o que a
    /// página precisa para se desenhar.
    /// </summary>
    private static readonly string[] Liberados =
    {
        PaginaDeTroca,
        "/Identity/Account/Logout",
        "/Identity/Account/AccessDenied"
    };

    private readonly RequestDelegate _next;

    public SenhaProvisoriaMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (PrecisaTrocar(context) && !EstaLiberado(context.Request.Path))
        {
            // Redirect, e não 403: quem está nesta situação não fez nada errado,
            // só precisa concluir o primeiro acesso.
            context.Response.Redirect(PaginaDeTroca);
            return;
        }

        await _next(context);
    }

    private static bool PrecisaTrocar(HttpContext context) =>
        context.User?.Identity?.IsAuthenticated == true &&
        context.User.HasClaim(c => c.Type == SenhaProvisoria && c.Value == "true");

    private static bool EstaLiberado(PathString caminho) =>
        Liberados.Any(p => caminho.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
}

public static class SenhaProvisoriaMiddlewareExtensions
{
    /// <summary>
    /// Deve entrar depois de UseAuthentication/UseAuthorization — antes disso o
    /// usuário ainda não tem identidade e a claim não existe.
    /// </summary>
    public static IApplicationBuilder UseSenhaProvisoria(this IApplicationBuilder app) =>
        app.UseMiddleware<SenhaProvisoriaMiddleware>();
}
