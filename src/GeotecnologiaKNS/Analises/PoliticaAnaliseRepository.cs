using System.Text.Json;
using GeotecnologiaKNS.Geo.Entities;

namespace GeotecnologiaKNS.Analises;

public interface IPoliticaAnaliseRepository
{
    /// <summary>
    /// Política da indústria, ou o protocolo padrão quando ela não definiu a sua.
    /// </summary>
    Task<PoliticaAnalise> ObterDoTenantAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria para a indústria uma cópia editável do protocolo padrão.
    /// </summary>
    /// <remarks>
    /// Copiar em vez de referenciar é deliberado: a partir daqui a indústria
    /// altera as próprias regras sem que mudanças no padrão a afetem de surpresa.
    /// </remarks>
    Task<PoliticaTenant> CriarAPartirDoPadraoAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acrescenta à política da indústria as regras que o protocolo ganhou
    /// depois de ela ter personalizado a sua. Devolve quantas entraram.
    /// </summary>
    Task<int> SincronizarComProtocoloAsync(int tenantId, CancellationToken cancellationToken = default);
}

public class PoliticaAnaliseRepository : IPoliticaAnaliseRepository
{
    private readonly ApplicationDbContext _context;

    public PoliticaAnaliseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliticaAnalise> ObterDoTenantAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var politica = await _context.Politicas
            .Include(x => x.Regras)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (politica is null)
        {
            return PoliticaAnalise.Padrao();
        }

        var regras = politica.Regras
            .Where(r => r.Ativa)
            .Select(r => new RegraAnalise
            {
                Codigo = r.Codigo,
                Descricao = r.Descricao,
                Tipo = r.Tipo,
                Restricao = r.Restricao,
                CadeiaIndireta = r.CadeiaIndireta,
                SituacaoDoCar = r.SituacaoDoCar,
                Severidade = (Severidade)r.Severidade,
                AreaMinimaHa = r.AreaMinimaHa,
                PercentualMinimo = r.PercentualMinimo,
                AnoMinimo = r.AnoMinimo,
                Fundamento = r.Fundamento
            })
            .ToList();

        // Compara com todas as regras da indústria, inclusive as desativadas:
        // uma regra que ela desligou de propósito não pode voltar como novidade
        // do protocolo. Comparar só com as ativas ressuscitava o que alguém
        // tinha decidido desligar.
        regras.AddRange(NovasDoProtocolo(politica.Regras.Select(r => r.Codigo)));

        return new PoliticaAnalise { Nome = politica.Nome, Regras = regras };
    }

    /// <summary>
    /// Regras que o protocolo ganhou depois que a indústria personalizou a dela.
    /// </summary>
    /// <remarks>
    /// Sem isto, toda regra nova nasce invisível para quem já tinha política
    /// própria — que é justamente a base de clientes mais antiga. A IND-001
    /// apareceu assim: a indústria adotou o padrão quando ele tinha dez regras,
    /// e a décima primeira nunca chegou nela.
    ///
    /// Entram sempre como informativas, mesmo que o protocolo as traga mais
    /// severas. Regra que a indústria ainda não revisou pode informar, não
    /// decidir: mudar o veredito de laudos por causa de uma regra que ninguém
    /// lá dentro leu seria pior que não entregar a regra. Ao abrir e salvar a
    /// tela de regras, ela passa a existir na política e o corte fica a cargo
    /// de quem responde pela conformidade.
    /// </remarks>
    private static IEnumerable<RegraAnalise> NovasDoProtocolo(IEnumerable<string> codigosDaIndustria)
    {
        var conhecidas = codigosDaIndustria.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return PoliticaAnalise.Padrao().Regras
            .Where(r => !conhecidas.Contains(r.Codigo))
            .Select(r => new RegraAnalise
            {
                Codigo = r.Codigo,
                Descricao = r.Descricao,
                Tipo = r.Tipo,
                Restricao = r.Restricao,
                CadeiaIndireta = r.CadeiaIndireta,
                SituacaoDoCar = r.SituacaoDoCar,
                Severidade = Severidade.Informativo,
                AreaMinimaHa = r.AreaMinimaHa,
                PercentualMinimo = r.PercentualMinimo,
                AnoMinimo = r.AnoMinimo,
                Fundamento = r.Fundamento
            });
    }

    /// <summary>
    /// Grava na política da indústria as regras novas do protocolo.
    /// </summary>
    /// <remarks>
    /// Chamada ao abrir a tela de regras, que é onde a indústria pode de fato
    /// revisá-las. Entram como informativas pelo mesmo motivo de
    /// <see cref="ObterDoTenantAsync"/>: regra que ninguém lá dentro leu ainda
    /// não pode mudar o veredito de um laudo.
    /// </remarks>
    public async Task<int> SincronizarComProtocoloAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var politica = await _context.Politicas
            .Include(x => x.Regras)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (politica is null)
        {
            // Sem política própria, a indústria já usa o protocolo inteiro.
            return 0;
        }

        var conhecidas = politica.Regras.Select(r => r.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var novas = PoliticaAnalise.Padrao().Regras
            .Where(r => !conhecidas.Contains(r.Codigo))
            .Select(r => new RegraTenant
            {
                Codigo = r.Codigo,
                Descricao = r.Descricao,
                Tipo = r.Tipo,
                Restricao = r.Restricao,
                CadeiaIndireta = r.CadeiaIndireta,
                SituacaoDoCar = r.SituacaoDoCar,
                Severidade = (int)Severidade.Informativo,
                AreaMinimaHa = r.AreaMinimaHa,
                PercentualMinimo = r.PercentualMinimo,
                AnoMinimo = r.AnoMinimo,
                Fundamento = r.Fundamento,
                Ativa = true
            })
            .ToList();

        if (novas.Count == 0)
        {
            return 0;
        }

        politica.Regras.AddRange(novas);
        await _context.SaveChangesAsync(cancellationToken);

        return novas.Count;
    }

    public async Task<PoliticaTenant> CriarAPartirDoPadraoAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var existente = await _context.Politicas
            .Include(x => x.Regras)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (existente is not null)
        {
            return existente;
        }

        var padrao = PoliticaAnalise.Padrao();

        var politica = new PoliticaTenant
        {
            TenantId = tenantId,
            Nome = padrao.Nome,
            AtualizadaEm = DateTime.Now,
            Regras = padrao.Regras.Select(r => new RegraTenant
            {
                Codigo = r.Codigo,
                Descricao = r.Descricao,
                Tipo = r.Tipo,
                Restricao = r.Restricao,
                CadeiaIndireta = r.CadeiaIndireta,
                SituacaoDoCar = r.SituacaoDoCar,
                Severidade = (int)r.Severidade,
                AreaMinimaHa = r.AreaMinimaHa,
                PercentualMinimo = r.PercentualMinimo,
                AnoMinimo = r.AnoMinimo,
                Fundamento = r.Fundamento,
                Ativa = true
            }).ToList()
        };

        _context.Politicas.Add(politica);
        await _context.SaveChangesAsync(cancellationToken);

        return politica;
    }
}

/// <summary>
/// Congela a política usada numa análise.
/// </summary>
/// <remarks>
/// Guardar só o nome não basta: a indústria pode afrouxar uma regra depois, e
/// um laudo precisa continuar explicando o veredito que deu na época. Sem o
/// retrato, um bloqueio contestado seis meses depois seria indefensável.
/// </remarks>
public static class RetratoDaPolitica
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = false };

    public static string Serializar(PoliticaAnalise politica)
    {
        var retrato = new
        {
            politica.Nome,
            Regras = politica.Regras.Select(r => new
            {
                r.Codigo,
                r.Descricao,
                Tipo = r.Tipo.ToString(),
                Severidade = r.Severidade.ToString(),
                r.AreaMinimaHa,
                r.PercentualMinimo,
                r.AnoMinimo,
                r.Fundamento
            })
        };

        return JsonSerializer.Serialize(retrato, Opcoes);
    }
}
