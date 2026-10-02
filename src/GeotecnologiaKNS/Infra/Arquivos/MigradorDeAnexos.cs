using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GeotecnologiaKNS.Infra.Arquivos;

/// <summary>
/// Move para o disco, uma vez, os anexos que ainda estão dentro do banco.
/// </summary>
/// <remarks>
/// Roda sozinho na subida em vez de ser um passo de instalação, porque passo de
/// instalação é passo que alguém esquece — e, esquecido, este deixaria o banco
/// exatamente do tamanho que a mudança existe para evitar.
///
/// É seguro rodar com o sistema no ar: cada documento é copiado para o disco
/// antes de a coluna ser zerada, e a leitura aceita os dois formatos enquanto a
/// migração acontece. Interromper no meio não perde nada — a próxima subida
/// continua de onde parou, porque o critério é a própria coluna preenchida.
/// </remarks>
public class MigradorDeAnexos : BackgroundService
{
    /// <summary>Documentos por rodada.</summary>
    /// <remarks>
    /// Pequeno de propósito: são até 10 MB cada, e um lote grande colocaria
    /// centenas de megabytes em memória para economizar segundos.
    /// </remarks>
    private const int TamanhoDoLote = 25;

    private readonly IServiceScopeFactory _escopos;
    private readonly IArmazenamentoDeArquivos _armazenamento;
    private readonly IOptions<OpcoesDeArquivos> _opcoes;
    private readonly ILogger<MigradorDeAnexos> _logger;

    public MigradorDeAnexos(
        IServiceScopeFactory escopos,
        IArmazenamentoDeArquivos armazenamento,
        IOptions<OpcoesDeArquivos> opcoes,
        ILogger<MigradorDeAnexos> logger)
    {
        _escopos = escopos;
        _armazenamento = armazenamento;
        _opcoes = opcoes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcoes.Value.MigrarAutomaticamente)
        {
            return;
        }

        try
        {
            // Deixa a aplicação terminar de subir antes de mexer em disco e
            // banco: atender a primeira requisição importa mais que mover
            // documento antigo.
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            var total = 0;

            total += await MigrarAsync<ProdutorArquivo>(c => c.ProdutoresArquivos, stoppingToken);
            total += await MigrarAsync<PropriedadeArquivo>(c => c.PropriedadesArquivos, stoppingToken);
            total += await MigrarAsync<AnaliseArquivo>(c => c.AnalisesArquivos, stoppingToken);

            if (total > 0)
            {
                _logger.LogInformation(
                    "{Total} anexo(s) movidos do banco para o armazenamento de arquivos.", total);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligamento normal.
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar a aplicação: os documentos
            // continuam legíveis na coluna, e a próxima subida tenta de novo.
            _logger.LogError(ex, "A migração de anexos não terminou. Os documentos seguem no banco.");
        }
    }

    private async Task<int> MigrarAsync<TArquivo>(
        Func<ApplicationDbContext, DbSet<TArquivo>> conjunto,
        CancellationToken stoppingToken)
        where TArquivo : Arquivo
    {
        var migrados = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var escopo = _escopos.CreateScope();

            var contexto = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // IgnoreQueryFilters: o serviço roda fora de qualquer sessão, e o
            // filtro por inquilino deixaria todos os documentos de fora — a
            // migração não migraria nada e nada avisaria.
            var lote = await conjunto(contexto)
                .IgnoreQueryFilters()
                .Where(x => x.Dados != null)
                .OrderBy(x => x.Id)
                .Take(TamanhoDoLote)
                .ToListAsync(stoppingToken);

            if (lote.Count == 0)
            {
                break;
            }

            foreach (var arquivo in lote)
            {
                await AnexoDeDocumento.PrepararAsync(
                    arquivo, _armazenamento, arquivo.TenantId, stoppingToken);
            }

            await contexto.SaveChangesAsync(stoppingToken);

            migrados += lote.Count;
        }

        return migrados;
    }
}
