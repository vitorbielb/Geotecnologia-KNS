using System.IO.Compression;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoPerimetro(int CamadaId, string Nome, int Feicoes, int Descartados);

public interface IPerimetroProprioService
{
    Task<IReadOnlyList<CamadaReferencia>> ListarAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<ResultadoPerimetro> ImportarAsync(
        string caminho, string nome, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> RemoverAsync(
        int camadaId, int tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Perímetros restritivos que a própria indústria define.
/// </summary>
/// <remarks>
/// Fecha a regra OUT-001, que até aqui era a única das dez sem base: ela existe
/// justamente para o que o protocolo público não cobre — a área de conflito que
/// a indústria conhece, o perímetro que um cliente dela exige, a fazenda que ela
/// decidiu não comprar. Sem um lugar para cadastrar isso, a regra ficava
/// permanentemente "não avaliada" em todo laudo.
///
/// Cada envio vira uma camada própria, e não uma recarga da anterior. É de
/// propósito: a indústria costuma ter mais de uma lista, com origens e validades
/// diferentes, e sobrescrever silenciosamente a anterior apagaria trabalho que
/// alguém fez. Remover é ação explícita.
/// </remarks>
public class PerimetroProprioService : IPerimetroProprioService
{
    /// <summary>Origem registrada no laudo para o que a indústria mesma subiu.</summary>
    private const string OrigemDeclarada = "Perímetro declarado pela indústria";

    private readonly GeoDbContext _context;
    private readonly CamadaShapefileImporter _shapefile;
    private readonly CamadaGeoJsonImporter _geoJson;
    private readonly ILogger<PerimetroProprioService> _logger;

    public PerimetroProprioService(
        GeoDbContext context,
        CamadaShapefileImporter shapefile,
        CamadaGeoJsonImporter geoJson,
        ILogger<PerimetroProprioService> logger)
    {
        _context = context;
        _shapefile = shapefile;
        _geoJson = geoJson;
        _logger = logger;
    }

    /// <summary>Perímetros da indústria, do mais recente para o mais antigo.</summary>
    public async Task<IReadOnlyList<CamadaReferencia>> ListarAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Camadas
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Tipo == TipoCamada.OutroPerimetro)
            .OrderByDescending(x => x.AtualizadaEm)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lê o arquivo enviado e publica o perímetro como camada da indústria.
    /// </summary>
    /// <param name="caminho">Arquivo já gravado em disco pelo controlador.</param>
    /// <param name="nome">Como o perímetro aparece no laudo.</param>
    public async Task<ResultadoPerimetro> ImportarAsync(
        string caminho,
        string nome,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId), "Perímetro próprio precisa de uma indústria dona.");
        }

        var pasta = Path.Combine(Path.GetTempPath(), $"perimetro-{Guid.NewGuid():N}");

        // A chave carrega o tenant por legibilidade na operação, mas quem isola
        // de verdade é a coluna tenant_id: chave é texto, e texto se digita
        // errado.
        var chave = $"perimetro-{tenantId}-{Guid.NewGuid():N}"[..40];

        try
        {
            Directory.CreateDirectory(pasta);

            var (arquivo, ehShapefile) = await PrepararAsync(caminho, pasta, cancellationToken);

            var resultado = ehShapefile
                ? await _shapefile.ImportarAsync(
                    arquivo, chave, nome, TipoCamada.OutroPerimetro, OrigemDeclarada,
                    anoReferencia: null, tenantId: tenantId,
                    biomas: null, cancellationToken: cancellationToken)
                : await _geoJson.ImportarAsync(
                    arquivo, chave, nome, TipoCamada.OutroPerimetro, OrigemDeclarada,
                    anoReferencia: null, tenantId: tenantId,
                    biomas: null, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Perímetro {Nome} da indústria {TenantId}: {Gravados} feições.",
                nome, tenantId, resultado.Gravados);

            return new ResultadoPerimetro(
                resultado.CamadaId, nome, resultado.Gravados, resultado.Descartados);
        }
        finally
        {
            Apagar(pasta);
        }
    }

    /// <summary>
    /// Remove um perímetro da indústria.
    /// </summary>
    /// <remarks>
    /// O tenant entra na condição de busca, e não numa conferência depois: a
    /// tela manda o id da camada, e sem isso bastaria chutar números para apagar
    /// o perímetro de uma concorrente — ou uma camada pública, que é de todos.
    /// </remarks>
    public async Task<bool> RemoverAsync(
        int camadaId, int tenantId, CancellationToken cancellationToken = default)
    {
        var camada = await _context.Camadas.FirstOrDefaultAsync(
            x => x.Id == camadaId
              && x.TenantId == tenantId
              && x.Tipo == TipoCamada.OutroPerimetro,
            cancellationToken);

        if (camada is null)
        {
            return false;
        }

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM geo.feicao_referencia WHERE camada_id = {0}",
            new object[] { camada.Id },
            cancellationToken);

        _context.Camadas.Remove(camada);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Perímetro {Nome} da indústria {TenantId} removido.", camada.Nome, tenantId);

        return true;
    }

    /// <summary>
    /// Descobre o que foi enviado e devolve o arquivo que o importador lê.
    /// </summary>
    /// <remarks>
    /// Shapefile não é um arquivo, são pelo menos três (.shp, .shx, .dbf) que
    /// precisam estar juntos — por isso só aceito o zip. Quem envia o .shp
    /// solto recebe isso dito com todas as letras, em vez de um erro de leitura
    /// que não explica nada.
    /// </remarks>
    private static async Task<(string Arquivo, bool EhShapefile)> PrepararAsync(
        string caminho, string pasta, CancellationToken cancellationToken)
    {
        if (!EhZip(caminho))
        {
            var extensao = Path.GetExtension(caminho);

            if (extensao.Equals(".shp", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Um shapefile são vários arquivos (.shp, .shx, .dbf, .prj) e o .shp sozinho " +
                    "não pode ser lido. Compacte a pasta inteira num .zip e envie o zip.");
            }

            await Task.CompletedTask;
            return (caminho, false);
        }

        var extraido = Path.Combine(pasta, "extraido");

        try
        {
            ZipFile.ExtractToDirectory(caminho, extraido);
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException("O arquivo enviado não é um zip válido.");
        }

        var shp = Maior(extraido, ".shp");

        if (shp is not null)
        {
            return (shp, true);
        }

        var geoJson = Maior(extraido, ".geojson", ".json");

        return geoJson is not null
            ? (geoJson, false)
            : throw new InvalidOperationException(
                "O zip não contém shapefile (.shp) nem GeoJSON (.geojson).");
    }

    private static bool EhZip(string caminho)
    {
        // Pela assinatura, não pela extensão: o navegador às vezes manda o zip
        // com outro nome, e confiar no nome faria a leitura falhar por um motivo
        // que não é o verdadeiro.
        using var fluxo = File.OpenRead(caminho);

        Span<byte> assinatura = stackalloc byte[2];

        return fluxo.ReadAtLeast(assinatura, 2, throwOnEndOfStream: false) == 2
               && assinatura[0] == 'P' && assinatura[1] == 'K';
    }

    private static string? Maior(string pasta, params string[] extensoes) =>
        Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories)
            .Where(x => extensoes.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(x => new FileInfo(x).Length)
            .FirstOrDefault();

    private static void Apagar(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta))
            {
                Directory.Delete(pasta, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporário preso não justifica desfazer uma importação que deu certo.
        }
    }
}
