using System.Security.Cryptography;
using System.Text;
using GeotecnologiaKNS.Geo.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace GeotecnologiaKNS.Laudos;

public record LaudoEmitido(byte[] Pdf, string NomeDoArquivo);

public interface IEmissorDeLaudo
{
    /// <summary>
    /// Emite o laudo de uma análise concluída, ou nulo quando ela não existe,
    /// não pertence à indústria ou ainda não terminou.
    /// </summary>
    Task<LaudoEmitido?> EmitirAsync(int analiseId, CancellationToken cancellationToken = default);
}

public class EmissorDeLaudo : IEmissorDeLaudo
{
    private readonly ApplicationDbContext _context;
    private readonly IIntersecaoService _intersecao;
    private readonly IMapaDoLaudo _mapa;
    private readonly ILogger<EmissorDeLaudo> _logger;

    public EmissorDeLaudo(
        ApplicationDbContext context,
        IIntersecaoService intersecao,
        IMapaDoLaudo mapa,
        ILogger<EmissorDeLaudo> logger)
    {
        _context = context;
        _intersecao = intersecao;
        _mapa = mapa;
        _logger = logger;
    }

    public async Task<LaudoEmitido?> EmitirAsync(
        int analiseId, CancellationToken cancellationToken = default)
    {
        // Sem IgnoreQueryFilters: o filtro global por indústria é o que impede
        // alguém de baixar o laudo de uma concorrente trocando o número na
        // URL. Aqui ele precisa valer.
        var analise = await _context.AnalisesAutomaticas
            .AsNoTracking()
            .Include(x => x.Ocorrencias)
            .FirstOrDefaultAsync(x => x.Id == analiseId, cancellationToken);

        if (analise is null || analise.Situacao != SituacaoAnalise.Concluida)
        {
            return null;
        }

        var solicitacao = await _context.Solicitacao
            .AsNoTracking()
            .Include(x => x.Propriedade!).ThenInclude(p => p.Produtor)
            .FirstOrDefaultAsync(x => x.Id == analise.SolicitacaoId, cancellationToken);

        if (solicitacao?.Propriedade is null)
        {
            return null;
        }

        var industria = await _context.Industrias
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == analise.TenantId, cancellationToken);

        var cadeia = await _context.FornecedoresIndiretos
            .AsNoTracking()
            .Where(x => x.PropriedadeId == solicitacao.PropriedadeId)
            .OrderBy(x => x.CodigoCar)
            .Select(x => new FornecedorDoLaudo(
                x.CodigoCar, x.NomeProdutor, x.Documento, x.Origem, x.DeclaradoEm))
            .ToListAsync(cancellationToken);

        var imagem = await MontarMapaAsync(
            solicitacao.Propriedade, analise.TenantId, cancellationToken);

        var dados = new DadosDoLaudo
        {
            Analise = analise,
            Solicitacao = solicitacao,
            Propriedade = solicitacao.Propriedade,
            Produtor = solicitacao.Propriedade.Produtor,
            Industria = industria,
            CadeiaIndireta = cadeia,
            Mapa = imagem.Png,
            MapaIndisponivel = imagem.Indisponivel,
            CodigoDeConferencia = Conferencia(analise)
        };

        var pdf = new LaudoPdf(dados).GeneratePdf();

        return new LaudoEmitido(pdf, $"laudo-{dados.Numero.Replace('/', '-')}.pdf");
    }

    /// <summary>
    /// Busca as geometrias de sobreposição e desenha o mapa.
    /// </summary>
    /// <remarks>
    /// Separado e tolerante a falha: o mapa é a evidência visual, não o laudo.
    /// Se a base geoespacial estiver fora do ar na hora da emissão, o documento
    /// sai dizendo que o mapa não pôde ser gerado — o que é bem melhor que não
    /// sair.
    /// </remarks>
    private async Task<ImagemDoMapa> MontarMapaAsync(
        Propriedade propriedade, int tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(propriedade.PerimetroGeoJson))
        {
            return new ImagemDoMapa(null, "o imóvel não tem perímetro registrado.");
        }

        IReadOnlyList<Sobreposicao> recortes = [];

        try
        {
            recortes = await _intersecao.ObterRecortesAsync(
                propriedade.CodigoCar, tenantId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "As sobreposições do laudo não puderam ser desenhadas para {Car}.",
                propriedade.CodigoCar);
        }

        return await _mapa.GerarAsync(propriedade.PerimetroGeoJson, recortes, cancellationToken);
    }

    /// <summary>
    /// Código curto derivado do conteúdo da análise.
    /// </summary>
    /// <remarks>
    /// Serve para conferir que o documento corresponde a uma análise registrada
    /// e para detectar um PDF adulterado: o mesmo laudo emitido duas vezes dá o
    /// mesmo código, e qualquer alteração no veredito, nas datas ou nas
    /// ocorrências dá outro.
    ///
    /// A data de emissão fica de fora de propósito — senão reemitir o mesmo
    /// laudo daria um código diferente, e o código deixaria de servir para
    /// conferir coisa alguma.
    ///
    /// Não é assinatura digital, e o laudo diz isso com todas as letras.
    /// </remarks>
    private static string Conferencia(AnaliseAutomatica analise)
    {
        var material = new StringBuilder()
            .Append(analise.Id).Append('|')
            .Append(analise.TenantId).Append('|')
            .Append(analise.CodigoCar).Append('|')
            .Append(analise.Resultado).Append('|')
            .Append(analise.ConcluidaEm?.ToString("O")).Append('|')
            .Append(analise.AreaImovelHa.ToString("F4")).Append('|')
            .Append(analise.CoberturaCompleta).Append('|')
            .Append(analise.PoliticaAplicada);

        foreach (var ocorrencia in analise.Ocorrencias.OrderBy(o => o.Id))
        {
            material.Append('|')
                    .Append(ocorrencia.CodigoRegra).Append(':')
                    .Append(ocorrencia.Severidade).Append(':')
                    .Append(ocorrencia.AreaSobrepostaHa.ToString("F4"));
        }

        var resumo = SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()));

        // Doze caracteres em grupos de quatro: curto o bastante para alguém
        // conferir no telefone, longo o bastante para não colidir.
        var texto = Convert.ToHexString(resumo)[..12];

        return $"{texto[..4]}-{texto[4..8]}-{texto[8..]}";
    }
}
