using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace GeotecnologiaKNS.Infra.Email;

/// <summary>
/// Envio das mensagens do sistema — hoje, a redefinição de senha.
/// </summary>
/// <remarks>
/// Até aqui o Identity resolvia IEmailSender com a implementação vazia que vem
/// na UI padrão: a tela de "esqueci minha senha" respondia normalmente, dizia
/// "verifique seu e-mail", e nada saía. O pior tipo de defeito, porque parece
/// que funcionou.
///
/// Três modos, e nenhum deles é silencioso. Sem configuração, o link de
/// redefinição vai para o log com um aviso de que o envio não está ligado —
/// assim dá para operar manualmente até o provedor entrar, em vez de ficar sem
/// saída nenhuma.
/// </remarks>
public class RemetenteDeEmail : IEmailSender
{
    private readonly OpcoesDeEmail _opcoes;
    private readonly ILogger<RemetenteDeEmail> _logger;

    public RemetenteDeEmail(IOptions<OpcoesDeEmail> opcoes, ILogger<RemetenteDeEmail> logger)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string assunto, string corpoHtml)
    {
        if (!_opcoes.EstaConfigurado())
        {
            RegistrarSemEnvio(email, assunto, corpoHtml);
            return;
        }

        var mensagem = Montar(email, assunto, corpoHtml);

        try
        {
            if (_opcoes.Entrega == EntregaDeEmail.Pasta)
            {
                await GravarEmPastaAsync(mensagem);
            }
            else
            {
                await EnviarPorSmtpAsync(mensagem);
            }

            _logger.LogInformation("Mensagem \"{Assunto}\" enviada para {Destinatario}.", assunto, email);
        }
        catch (Exception ex)
        {
            // Não relançar: quem pediu a redefinição não deve receber um erro de
            // servidor por causa do provedor de e-mail, e a página do Identity
            // responde igual para endereço existente ou não — de propósito, para
            // não revelar quem tem conta. O link fica no log para socorro manual.
            _logger.LogError(ex,
                "Falha ao enviar \"{Assunto}\" para {Destinatario}. Conteúdo: {Corpo}",
                assunto, email, corpoHtml);
        }
    }

    private void RegistrarSemEnvio(string email, string assunto, string corpoHtml)
    {
        _logger.LogWarning(
            "Envio de e-mail não configurado (Email:Entrega = Nenhuma). " +
            "A mensagem \"{Assunto}\" para {Destinatario} NÃO foi enviada. Conteúdo: {Corpo}",
            assunto, email, corpoHtml);
    }

    private MimeMessage Montar(string destinatario, string assunto, string corpoHtml)
    {
        var mensagem = new MimeMessage();

        mensagem.From.Add(new MailboxAddress(
            _opcoes.RemetenteNome,
            string.IsNullOrWhiteSpace(_opcoes.RemetenteEndereco)
                ? "nao-responda@localhost"
                : _opcoes.RemetenteEndereco));

        mensagem.To.Add(MailboxAddress.Parse(destinatario));
        mensagem.Subject = assunto;
        mensagem.Body = new TextPart(TextFormat.Html) { Text = corpoHtml };

        return mensagem;
    }

    private async Task GravarEmPastaAsync(MimeMessage mensagem)
    {
        Directory.CreateDirectory(_opcoes.Pasta);

        var nome = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.eml"[..40] + ".eml";
        var caminho = Path.Combine(_opcoes.Pasta, nome);

        await using var arquivo = File.Create(caminho);
        await mensagem.WriteToAsync(arquivo);

        _logger.LogInformation("Mensagem gravada em {Caminho}.", caminho);
    }

    private async Task EnviarPorSmtpAsync(MimeMessage mensagem)
    {
        using var cliente = new SmtpClient();

        // StartTls na 587 e SslOnConnect na 465 são o que os provedores
        // transacionais esperam; Auto escolhe pelo que o servidor anuncia.
        var seguranca = _opcoes.UsarTls
            ? SecureSocketOptions.Auto
            : SecureSocketOptions.None;

        await cliente.ConnectAsync(_opcoes.Servidor, _opcoes.Porta, seguranca);

        if (!string.IsNullOrWhiteSpace(_opcoes.Usuario))
        {
            await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha);
        }

        await cliente.SendAsync(mensagem);
        await cliente.DisconnectAsync(quit: true);
    }
}
