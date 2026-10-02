using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoDaRecarga(
    string Chave,
    bool Sucesso,
    string Mensagem,
    TimeSpan Duracao);

/// <summary>
/// Baixa a camada na origem e a recarrega, sem ninguém presente.
/// </summary>
/// <remarks>
/// A recarga desassistida só é segura por causa de duas coisas que vêm antes
/// dela: a troca versionada, que impede que uma carga interrompida deixe a
/// camada pela metade, e a conferência de <see cref="GuardaDeCarga"/>, que
/// recusa arquivo truncado na origem. Sem as duas, automatizar isto seria
/// automatizar o estrago — o sistema erraria sozinho, de madrugada, e
/// continuaria emitindo laudos com a mesma confiança de antes.
///
/// O que pode dar errado aqui — origem fora do ar, download pela metade,
/// arquivo corrompido, layout mudado — termina sempre do mesmo jeito: a versão
/// anterior continua no ar e a falha fica registrada. Nunca em camada vazia.
/// </remarks>
public class RecarregadorDeCamadas
{
    /// <summary>
    /// O CSV do IBAMA tem 209 MB e a origem é lenta; o padrão de 100s não dá.
    /// </summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(40) };

    private readonly GeoDbContext _context;
    private readonly CamadaShapefileImporter _shapefile;
    private readonly CamadaGeoJsonImporter _geoJson;
    private readonly EmbargoIbamaImporter _embargo;
    private readonly ILogger<RecarregadorDeCamadas> _logger;

    public RecarregadorDeCamadas(
        GeoDbContext context,
        CamadaShapefileImporter shapefile,
        CamadaGeoJsonImporter geoJson,
        EmbargoIbamaImporter embargo,
        ILogger<RecarregadorDeCamadas> logger)
    {
        _context = context;
        _shapefile = shapefile;
        _geoJson = geoJson;
        _embargo = embargo;
        _logger = logger;
    }

    /// <summary>
    /// Grava no banco a periodicidade declarada no catálogo.
    /// </summary>
    /// <remarks>
    /// As camadas carregadas antes de o catálogo existir não têm prazo nenhum —
    /// e sem prazo nenhuma delas jamais venceria, o que faria a recarga
    /// automática nunca disparar e parecer que está tudo em dia.
    /// </remarks>
    public async Task SincronizarCatalogoAsync(CancellationToken cancellationToken = default)
    {
        var camadas = await _context.Camadas.ToListAsync(cancellationToken);
        var mudou = false;

        foreach (var camada in camadas)
        {
            var fonte = CatalogoDeFontes.PorChave(camada.Chave);

            if (fonte is null || camada.PeriodicidadeDias == fonte.PeriodicidadeDias)
            {
                continue;
            }

            camada.PeriodicidadeDias = fonte.PeriodicidadeDias;
            mudou = true;
        }

        if (mudou)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Fontes cuja camada passou do prazo — ou que nunca foram carregadas.
    /// </summary>
    public async Task<IReadOnlyList<FonteDeCamada>> ObterVencidasAsync(
        CancellationToken cancellationToken = default)
    {
        var camadas = await _context.Camadas
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Chave, cancellationToken);

        return CatalogoDeFontes.Todas
            .Where(fonte => !camadas.TryGetValue(fonte.Chave, out var camada)
                            || camada.AtualizadaEm is null
                            || camada.AtualizadaEm.Value.AddDays(fonte.PeriodicidadeDias) < DateTime.UtcNow)
            .ToList();
    }

    /// <summary>
    /// Listas por documento vencidas que ninguém recarrega sozinho.
    /// </summary>
    /// <remarks>
    /// O Cadastro de Empregadores do MTE sai em PDF, e lê-lo depende do
    /// <c>pdftotext</c> instalado na máquina. Automatizar isso significaria
    /// pendurar a aplicação num binário externo, e preferi não decidir isso
    /// sozinho — mas uma lista restritiva envelhecendo em silêncio é exatamente
    /// o que este trabalho todo existe para impedir. Então ela vence, aparece, e
    /// alguém é avisado.
    /// </remarks>
    public async Task<IReadOnlyList<ListaRestritiva>> ObterListasVencidasAsync(
        CancellationToken cancellationToken = default)
    {
        var listas = await _context.ListasRestritivas.AsNoTracking().ToListAsync(cancellationToken);

        // A lista de embargos sai do mesmo CSV da camada e é recarregada junto
        // com ela; as demais dependem de alguém.
        return listas
            .Where(x => x.Tipo != TipoRestricao.EmbargoAmbiental && x.Vencida)
            .ToList();
    }

    /// <summary>
    /// Baixa e recarrega uma camada. Não lança: falha vira resultado.
    /// </summary>
    /// <remarks>
    /// Não lançar é deliberado. Quem chama é um laço que percorre várias fontes,
    /// e uma origem fora do ar não pode impedir que as outras sejam atualizadas.
    /// </remarks>
    public async Task<ResultadoDaRecarga> RecarregarAsync(
        FonteDeCamada fonte,
        string? pastaDeTrabalho = null,
        CancellationToken cancellationToken = default)
    {
        var relogio = Stopwatch.StartNew();

        var pasta = Path.Combine(
            pastaDeTrabalho ?? Path.Combine(Path.GetTempPath(), "geotecnologiakns-recarga"),
            $"{fonte.Chave}-{Guid.NewGuid():N}");

        // A trava impede que duas recargas da mesma camada rodem ao mesmo tempo
        // — o agendador da aplicação e alguém na linha de comando, por exemplo.
        // Sem ela as duas gravariam na mesma versão seguinte, e a contagem final
        // sairia somada: passaria pela conferência sem ninguém notar.
        if (!await TentarTravarAsync(fonte.Chave, cancellationToken))
        {
            return new ResultadoDaRecarga(
                fonte.Chave, false, "Já existe uma recarga desta camada em andamento.", relogio.Elapsed);
        }

        try
        {
            Directory.CreateDirectory(pasta);

            // Percorre a fonte e as reservas dela. Uma origem pública fora do ar
            // não pode significar camada envelhecendo em silêncio, que é a falha
            // que não se parece com falha nenhuma.
            Exception? ultimaFalha = null;

            foreach (var tentativa in fonte.ComAsReservas())
            {
                try
                {
                    var mensagem = await BaixarEImportarAsync(tentativa, pasta, cancellationToken);

                    _logger.LogInformation(
                        "Recarga de {Chave} concluída em {Segundos:N0}s: {Mensagem}",
                        fonte.Chave, relogio.Elapsed.TotalSeconds, mensagem);

                    return new ResultadoDaRecarga(fonte.Chave, true, mensagem, relogio.Elapsed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ultimaFalha = ex;

                    if (tentativa.Reserva is not null)
                    {
                        _logger.LogWarning(
                            ex, "{Origem} falhou para {Chave}; tentando a reserva.",
                            tentativa.Origem, fonte.Chave);
                    }
                }
            }

            _logger.LogError(
                ultimaFalha, "Recarga de {Chave} falhou. A versão anterior continua no ar.", fonte.Chave);

            return new ResultadoDaRecarga(
                fonte.Chave, false, ultimaFalha?.Message ?? "Falha desconhecida.", relogio.Elapsed);
        }
        finally
        {
            await DestravarAsync(fonte.Chave);
            Apagar(pasta);
        }
    }

    private async Task<string> BaixarEImportarAsync(
        FonteDeCamada fonte, string pasta, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recarregando {Chave} a partir de {Url}", fonte.Chave, fonte.UrlResolvida());

        // Subpasta por tentativa: a reserva não pode achar o arquivo meio
        // baixado da tentativa anterior e tomá-lo por bom.
        var daTentativa = Path.Combine(pasta, Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(daTentativa);

        var baixado = await BaixarAsync(fonte, daTentativa, cancellationToken);
        var arquivo = await PrepararArquivoAsync(fonte, baixado, daTentativa, cancellationToken);

        return await ImportarAsync(fonte, arquivo, cancellationToken);
    }

    private async Task<string> BaixarAsync(
        FonteDeCamada fonte, string pasta, CancellationToken cancellationToken)
    {
        using var requisicao = new HttpRequestMessage(
            fonte.EhPost ? HttpMethod.Post : HttpMethod.Get, fonte.UrlResolvida());

        if (fonte.EhPost)
        {
            requisicao.Content = new StringContent(fonte.CorpoJson!, Encoding.UTF8, "application/json");
        }

        using var resposta = await Http.SendAsync(
            requisicao, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"A origem respondeu {(int)resposta.StatusCode} {resposta.ReasonPhrase}.");
        }

        var destino = Path.Combine(
            pasta, fonte.Formato == FormatoDaFonte.CsvEmbargo ? "origem.csv" : "origem.zip");

        await using (var saida = File.Create(destino))
        await using (var entrada = await resposta.Content.ReadAsStreamAsync(cancellationToken))
        {
            await entrada.CopyToAsync(saida, cancellationToken);
        }

        var bytes = new FileInfo(destino).Length;

        _logger.LogInformation("{Chave}: {Mb:N1} MB baixados.", fonte.Chave, bytes / 1024.0 / 1024.0);

        return destino;
    }

    /// <summary>
    /// Descompacta quando é o caso e devolve o arquivo que o importador lê.
    /// </summary>
    /// <remarks>
    /// A conferência do conteúdo acontece aqui porque os GeoServer da FUNAI e do
    /// INPE respondem <c>200 OK</c> com uma mensagem de erro XML no corpo quando
    /// a consulta não agrada. Sem olhar o que chegou, isso seguiria adiante como
    /// "zip corrompido" e ninguém saberia que a origem explicou o problema.
    /// </remarks>
    private async Task<string> PrepararArquivoAsync(
        FonteDeCamada fonte, string baixado, string pasta, CancellationToken cancellationToken)
    {
        if (fonte.Formato == FormatoDaFonte.CsvEmbargo)
        {
            using var leitor = new StreamReader(baixado, Encoding.Latin1);
            var cabecalho = await leitor.ReadLineAsync(cancellationToken) ?? string.Empty;

            if (!cabecalho.Contains("GEOM_AREA_EMBARGADA", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "O CSV do IBAMA não tem a coluna GEOM_AREA_EMBARGADA. " +
                    $"A primeira linha recebida foi: {Recortar(cabecalho)}");
            }

            return baixado;
        }

        var extraido = Path.Combine(pasta, "extraido");

        try
        {
            ZipFile.ExtractToDirectory(baixado, extraido);
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException(
                "O que a origem devolveu não é um zip. " +
                $"O começo do arquivo é: {Recortar(LerComeco(baixado))}");
        }

        var extensoes = fonte.Formato == FormatoDaFonte.GeoJsonEmZip
            ? new[] { ".geojson", ".json" }
            : new[] { ".shp" };

        // O maior, e não o primeiro: o zip do INCRA traz arquivos auxiliares
        // junto, e o nome do principal muda entre publicações. Escolher pelo
        // tamanho sobrevive a uma renomeação na origem.
        var arquivo = Directory.EnumerateFiles(extraido, "*", SearchOption.AllDirectories)
            .Where(x => extensoes.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(x => new FileInfo(x).Length)
            .FirstOrDefault();

        return arquivo ?? throw new InvalidOperationException(
            $"O zip baixado não contém nenhum arquivo {string.Join(" ou ", extensoes)}.");
    }

    private async Task<string> ImportarAsync(
        FonteDeCamada fonte, string arquivo, CancellationToken cancellationToken)
    {
        switch (fonte.Formato)
        {
            case FormatoDaFonte.CsvEmbargo:
            {
                var resultado = await _embargo.ImportarAsync(arquivo, cancellationToken);

                return $"{resultado.Gravados:N0} polígonos e " +
                       $"{resultado.Documentos:N0} registros por documento.";
            }

            case FormatoDaFonte.GeoJsonEmZip:
            {
                var resultado = await _geoJson.ImportarAsync(
                    arquivo, fonte.Chave, fonte.Nome, fonte.Tipo, fonte.Origem,
                    fonte.AnoReferencia, tenantId: null, cancellationToken);

                return $"{resultado.Gravados:N0} feições ({resultado.Descartados:N0} descartadas).";
            }

            default:
            {
                var resultado = await _shapefile.ImportarAsync(
                    arquivo, fonte.Chave, fonte.Nome, fonte.Tipo, fonte.Origem,
                    fonte.AnoReferencia, tenantId: null, cancellationToken);

                return $"{resultado.Gravados:N0} feições ({resultado.Descartados:N0} descartadas).";
            }
        }
    }

    private async Task<bool> TentarTravarAsync(string chave, CancellationToken cancellationToken)
    {
        // A trava é da sessão do PostgreSQL, então a conexão precisa ficar
        // aberta até o fim; sem isto o EF a devolveria ao pool entre comandos e
        // a trava cairia junto.
        await _context.Database.OpenConnectionAsync(cancellationToken);

        await using var comando = CriarComando("SELECT pg_try_advisory_lock(hashtext(@chave)::bigint)", chave);

        var travou = (bool)(await comando.ExecuteScalarAsync(cancellationToken) ?? false);

        if (!travou)
        {
            await _context.Database.CloseConnectionAsync();
        }

        return travou;
    }

    private async Task DestravarAsync(string chave)
    {
        try
        {
            await using var comando = CriarComando("SELECT pg_advisory_unlock(hashtext(@chave)::bigint)", chave);
            await comando.ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            // A trava morre junto com a conexão; falhar ao soltá-la não é motivo
            // para transformar uma recarga bem-sucedida em erro.
            _logger.LogWarning(ex, "Não consegui soltar a trava de {Chave}.", chave);
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private NpgsqlCommand CriarComando(string sql, string chave)
    {
        var conexao = (NpgsqlConnection)_context.Database.GetDbConnection();
        var comando = conexao.CreateCommand();

        comando.CommandText = sql;
        comando.Parameters.AddWithValue("chave", $"recarga:{chave}");

        return comando;
    }

    private static string LerComeco(string caminho)
    {
        using var leitor = new StreamReader(caminho);
        var buffer = new char[400];
        var lidos = leitor.ReadBlock(buffer, 0, buffer.Length);

        return new string(buffer, 0, lidos);
    }

    private static string Recortar(string texto)
    {
        var limpo = texto.ReplaceLineEndings(" ").Trim();

        return limpo.Length <= 300 ? limpo : limpo[..300] + "…";
    }

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
            // Arquivo temporário preso não justifica falhar a recarga.
        }
    }
}
