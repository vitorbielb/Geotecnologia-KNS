using Microsoft.Extensions.Options;

namespace GeotecnologiaKNS.Infra.Arquivos;

/// <summary>
/// Onde ficam os documentos anexados.
/// </summary>
/// <remarks>
/// Existe como interface porque o destino é uma decisão de infraestrutura, não
/// de domínio: hoje é o disco do servidor, e trocar por S3 ou Azure Blob é
/// escrever outra implementação sem tocar em controlador nenhum.
///
/// O que motivou tirar do banco: os anexos moravam numa coluna
/// <c>varbinary(max)</c>, então cada backup do SQL Server carregava junto todo
/// PDF que algum analista já subiu. Com volume de produção, o backup fica lento
/// a ponto de deixar de ser feito — e backup que não se faz é o mesmo que não
/// ter. A base passa a guardar só o ponteiro.
/// </remarks>
public interface IArmazenamentoDeArquivos
{
    /// <summary>Grava o conteúdo e devolve a chave que o localiza depois.</summary>
    Task<string> GravarAsync(
        Stream conteudo, int tenantId, string nomeOriginal, CancellationToken cancellationToken = default);

    /// <summary>Abre o conteúdo, ou devolve nulo se a chave não existe mais.</summary>
    Task<Stream?> AbrirAsync(string chave, CancellationToken cancellationToken = default);

    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);
}

public class OpcoesDeArquivos
{
    public const string Secao = "Arquivos";

    /// <summary>
    /// Pasta onde os anexos são gravados.
    /// </summary>
    /// <remarks>
    /// Vazio usa <c>App_Data/arquivos</c> sob a raiz da aplicação, que funciona
    /// sem configurar nada. Em produção vale apontar para um volume separado —
    /// e lembrar que, a partir daqui, o backup do banco sozinho não basta.
    /// </remarks>
    public string Raiz { get; set; } = string.Empty;

    /// <summary>
    /// Move para o disco, na subida, os anexos que ainda estão no banco.
    /// </summary>
    public bool MigrarAutomaticamente { get; set; } = true;
}

/// <summary>
/// Guarda os anexos no disco do servidor.
/// </summary>
public class ArmazenamentoEmDisco : IArmazenamentoDeArquivos
{
    private readonly string _raiz;
    private readonly ILogger<ArmazenamentoEmDisco> _logger;

    public ArmazenamentoEmDisco(
        IOptions<OpcoesDeArquivos> opcoes,
        IWebHostEnvironment ambiente,
        ILogger<ArmazenamentoEmDisco> logger)
    {
        var configurada = opcoes.Value.Raiz;

        _raiz = Path.GetFullPath(string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(ambiente.ContentRootPath, "App_Data", "arquivos")
            : configurada);

        _logger = logger;

        Directory.CreateDirectory(_raiz);
    }

    public async Task<string> GravarAsync(
        Stream conteudo, int tenantId, string nomeOriginal, CancellationToken cancellationToken = default)
    {
        // A indústria e a data entram no caminho para que a pasta continue
        // navegável por um humano em dia de incidente, e para que nenhum
        // diretório acumule arquivos indefinidamente.
        var relativo = Path.Combine(
            tenantId.ToString(),
            DateTime.UtcNow.ToString("yyyy-MM"),
            $"{Guid.NewGuid():N}{ExtensaoSegura(nomeOriginal)}");

        var destino = Path.Combine(_raiz, relativo);

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        await using (var saida = File.Create(destino))
        {
            await conteudo.CopyToAsync(saida, cancellationToken);
        }

        // Barra normal na chave: ela é gravada no banco e precisa continuar
        // válida se o sistema operacional do servidor mudar.
        return relativo.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream?> AbrirAsync(string chave, CancellationToken cancellationToken = default)
    {
        var caminho = Resolver(chave);

        if (caminho is null || !File.Exists(caminho))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(
            new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true));
    }

    public Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        var caminho = Resolver(chave);

        try
        {
            if (caminho is not null && File.Exists(caminho))
            {
                File.Delete(caminho);
            }
        }
        catch (IOException ex)
        {
            // Arquivo órfão no disco é desperdício; linha apagada que volta a
            // aparecer seria defeito. Entre os dois, prefiro o desperdício.
            _logger.LogWarning(ex, "Não consegui apagar o anexo {Chave}.", chave);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Converte a chave em caminho, recusando qualquer coisa fora da raiz.
    /// </summary>
    /// <remarks>
    /// A chave é gerada aqui e nunca vem do usuário, mas ela trafega pelo banco
    /// — e uma linha adulterada com <c>../../</c> leria arquivo de configuração
    /// do servidor pela tela de documentos. A conferência custa uma comparação
    /// de texto e fecha a porta de vez.
    /// </remarks>
    private string? Resolver(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        var completo = Path.GetFullPath(Path.Combine(_raiz, chave));

        if (!completo.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Chave de anexo fora da raiz de armazenamento: {Chave}", chave);
            return null;
        }

        return completo;
    }

    private static string ExtensaoSegura(string nomeOriginal)
    {
        var extensao = Path.GetExtension(nomeOriginal);

        // Só letras e dígitos, no máximo dez: a extensão vai para o nome de um
        // arquivo real, e o nome original vem de quem enviou.
        return extensao.Length is > 1 and <= 10 && extensao[1..].All(char.IsLetterOrDigit)
            ? extensao.ToLowerInvariant()
            : string.Empty;
    }
}
