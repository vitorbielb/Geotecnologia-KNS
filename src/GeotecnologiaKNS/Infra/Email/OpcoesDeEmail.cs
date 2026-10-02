namespace GeotecnologiaKNS.Infra.Email;

/// <summary>Como as mensagens saem do sistema.</summary>
public enum EntregaDeEmail
{
    /// <summary>
    /// Nada é enviado. É o padrão, e é deliberado: sem configuração, o sistema
    /// registra em log o que teria mandado em vez de fingir que mandou.
    /// </summary>
    Nenhuma = 0,

    /// <summary>Grava a mensagem em disco, como .eml. Para desenvolvimento.</summary>
    Pasta = 1,

    /// <summary>Envio real por SMTP.</summary>
    Smtp = 2
}

public class OpcoesDeEmail
{
    public const string SecaoDeConfiguracao = "Email";

    public EntregaDeEmail Entrega { get; set; } = EntregaDeEmail.Nenhuma;

    /// <summary>Endereço que aparece como remetente.</summary>
    public string RemetenteEndereco { get; set; } = string.Empty;

    public string RemetenteNome { get; set; } = "KNS Ambiental";

    public string Servidor { get; set; } = string.Empty;

    public int Porta { get; set; } = 587;

    /// <summary>
    /// STARTTLS na 587, TLS direto na 465. Desligar só faz sentido contra um
    /// servidor de teste na própria máquina.
    /// </summary>
    public bool UsarTls { get; set; } = true;

    public string Usuario { get; set; } = string.Empty;

    public string Senha { get; set; } = string.Empty;

    /// <summary>Destino dos arquivos quando a entrega é em pasta.</summary>
    public string Pasta { get; set; } = "correspondencia";

    /// <summary>
    /// Indica se há configuração suficiente para o modo escolhido.
    /// </summary>
    public bool EstaConfigurado() => Entrega switch
    {
        EntregaDeEmail.Smtp => !string.IsNullOrWhiteSpace(Servidor)
                               && !string.IsNullOrWhiteSpace(RemetenteEndereco),
        EntregaDeEmail.Pasta => !string.IsNullOrWhiteSpace(Pasta),
        _ => false
    };
}
