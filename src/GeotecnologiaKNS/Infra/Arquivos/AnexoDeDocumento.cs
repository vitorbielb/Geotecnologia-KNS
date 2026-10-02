using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Infra.Arquivos;

/// <summary>
/// A ponte entre a tela de documentos e o armazenamento.
/// </summary>
/// <remarks>
/// Os três controladores que lidam com anexo — produtor, propriedade e análise
/// — fazem exatamente a mesma coisa com ele. Centralizar aqui evita que as três
/// cópias divirjam justamente no trecho em que uma divergência vira
/// vazamento ou documento perdido.
/// </remarks>
public static class AnexoDeDocumento
{
    /// <summary>
    /// Grava o conteúdo recebido no armazenamento e deixa a linha só com o ponteiro.
    /// </summary>
    public static async Task PrepararAsync(
        Arquivo arquivo,
        IArmazenamentoDeArquivos armazenamento,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        if (arquivo.Dados is not { Length: > 0 } conteudo)
        {
            return;
        }

        await using var fluxo = new MemoryStream(conteudo, writable: false);

        arquivo.Chave = await armazenamento.GravarAsync(
            fluxo, tenantId, arquivo.Descricao ?? string.Empty, cancellationToken);

        arquivo.Tamanho = conteudo.Length;

        // Zerado logo depois de gravado: deixar os dois preenchidos faria o
        // backup continuar carregando o que a mudança existe para tirar de lá.
        arquivo.Dados = null;
    }

    /// <summary>
    /// Devolve o conteúdo do anexo para download.
    /// </summary>
    /// <remarks>
    /// Documento antigo ainda mora na coluna do banco, e vai continuar morando
    /// até a migração passar por ele. Ler os dois casos aqui é o que permite a
    /// migração acontecer com o sistema no ar, sem janela em que um documento
    /// fica inacessível.
    /// </remarks>
    public static async Task<ActionResult> ResponderAsync(
        Arquivo arquivo,
        IArmazenamentoDeArquivos armazenamento,
        CancellationToken cancellationToken = default)
    {
        if (arquivo.Dados is { Length: > 0 } legado)
        {
            return new FileContentResult(legado, arquivo.ContentType);
        }

        if (string.IsNullOrWhiteSpace(arquivo.Chave))
        {
            return new NotFoundResult();
        }

        var fluxo = await armazenamento.AbrirAsync(arquivo.Chave, cancellationToken);

        // Nulo aqui significa linha apontando para arquivo que sumiu do disco.
        // NotFound, e não erro: a tela já trata ausência, e um 500 só esconderia
        // o problema real no meio do monitoramento.
        return fluxo is null
            ? new NotFoundResult()
            : new FileStreamResult(fluxo, arquivo.ContentType);
    }

    /// <summary>Apaga o conteúdo depois que a linha já saiu do banco.</summary>
    public static Task DescartarAsync(
        Arquivo arquivo,
        IArmazenamentoDeArquivos armazenamento,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(arquivo.Chave)
            ? Task.CompletedTask
            : armazenamento.RemoverAsync(arquivo.Chave, cancellationToken);
    }
}
