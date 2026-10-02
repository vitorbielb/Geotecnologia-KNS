namespace GeotecnologiaKNS.Services;

/// <summary>Consumo de uma indústria numa competência.</summary>
public record ConsumoMensal(
    int TenantId,
    string Industria,
    int Competencia,
    int Analises,
    int Solicitacoes,
    int Imoveis)
{
    public string CompetenciaFormatada =>
        $"{Competencia % 100:00}/{Competencia / 100}";

    public int Total => Analises + Solicitacoes + Imoveis;
}

public interface IMedidorDeUso
{
    /// <summary>Registra um fato cobrável.</summary>
    Task RegistrarAsync(
        int tenantId,
        TipoDeUso tipo,
        int referenciaId,
        string? descricao = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumo por indústria e competência, da mais recente para a mais antiga.
    /// </summary>
    Task<IReadOnlyList<ConsumoMensal>> ObterConsumoAsync(
        int? competenciaMinima = null,
        CancellationToken cancellationToken = default);
}

public class MedidorDeUso : IMedidorDeUso
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MedidorDeUso> _logger;

    public MedidorDeUso(ApplicationDbContext context, ILogger<MedidorDeUso> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        int tenantId,
        TipoDeUso tipo,
        int referenciaId,
        string? descricao = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            // Sem inquilino não há a quem cobrar. Acontece em rotinas internas;
            // registrar com tenant zero só sujaria a apuração.
            return;
        }

        var agora = DateTime.UtcNow;

        _context.EventosDeUso.Add(new EventoDeUso
        {
            TenantId = tenantId,
            Tipo = tipo,
            ReferenciaId = referenciaId,
            Descricao = descricao,
            OcorridoEm = agora,
            Competencia = EventoDeUso.CompetenciaDe(agora)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Medição não pode derrubar a operação que a originou: perder um
            // evento custa uma linha de fatura, perder a análise custa o
            // trabalho do usuário. Fica no log para conciliação.
            _logger.LogError(ex,
                "Falha ao registrar uso {Tipo} do inquilino {TenantId}, referência {ReferenciaId}.",
                tipo, tenantId, referenciaId);
        }
    }

    public async Task<IReadOnlyList<ConsumoMensal>> ObterConsumoAsync(
        int? competenciaMinima = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _context.EventosDeUso.AsNoTracking();

        if (competenciaMinima.HasValue)
        {
            consulta = consulta.Where(x => x.Competencia >= competenciaMinima.Value);
        }

        var agrupado = await consulta
            .GroupBy(x => new { x.TenantId, x.Competencia })
            .Select(g => new
            {
                g.Key.TenantId,
                g.Key.Competencia,
                Analises = g.Count(x => x.Tipo == TipoDeUso.AnaliseExecutada),
                Solicitacoes = g.Count(x => x.Tipo == TipoDeUso.SolicitacaoAberta),
                Imoveis = g.Count(x => x.Tipo == TipoDeUso.ImovelCadastrado)
            })
            .ToListAsync(cancellationToken);

        // O nome da indústria vem numa segunda consulta porque o filtro global
        // de inquilino não se aplica a Industrias: juntar as duas no banco
        // devolveria nome nulo para quem enxerga mais de um inquilino.
        var nomes = await _context.Industrias
            .AsNoTracking()
            .ToDictionaryAsync(x => x.TenantId, x => x.NomeResumido ?? x.Nome, cancellationToken);

        return agrupado
            .OrderByDescending(x => x.Competencia)
            .ThenBy(x => x.TenantId)
            .Select(x => new ConsumoMensal(
                x.TenantId,
                nomes.TryGetValue(x.TenantId, out var nome) ? nome : $"Inquilino {x.TenantId}",
                x.Competencia,
                x.Analises,
                x.Solicitacoes,
                x.Imoveis))
            .ToList();
    }
}
