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

        return new PoliticaAnalise
        {
            Nome = politica.Nome,
            Regras = politica.Regras
                .Where(r => r.Ativa)
                .Select(r => new RegraAnalise
                {
                    Codigo = r.Codigo,
                    Descricao = r.Descricao,
                    Tipo = r.Tipo,
                    Severidade = (Severidade)r.Severidade,
                    AreaMinimaHa = r.AreaMinimaHa,
                    PercentualMinimo = r.PercentualMinimo,
                    AnoMinimo = r.AnoMinimo,
                    Fundamento = r.Fundamento
                })
                .ToList()
        };
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
