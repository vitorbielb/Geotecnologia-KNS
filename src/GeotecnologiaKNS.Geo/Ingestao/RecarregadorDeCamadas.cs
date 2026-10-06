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

            // Os limites de biomas primeiro: é deles que as camadas regionais
            // tiram o recorte que declaram cobrir, e uma camada regional que
            // não consegue declará-lo não é publicada. Deixar essa ordem por
            // conta da posição no catálogo funcionaria hoje e quebraria na
            // primeira vez que alguém reordenasse a lista — sem erro de
            // compilação e sem nada falhar até a próxima recarga.
            .OrderByDescending(fonte => fonte.Tipo == TipoCamada.Bioma)
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

        // Quanto a origem diz ter, antes de baixar. É com este número que a
        // carga é conferida no fim — e é o que teria denunciado, na hora, o
        // PRODES que entrou no ar com 50.000 de 802.277 polígonos.
        var esperado = await ContarNaOrigemAsync(fonte, cancellationToken);

        var arquivos = await BaixarTudoAsync(fonte, daTentativa, cancellationToken);

        return await ImportarAsync(fonte, arquivos, esperado, cancellationToken);
    }

    /// <summary>
    /// Baixa a camada inteira: uma requisição, ou tantas quantas forem precisas.
    /// </summary>
    /// <remarks>
    /// A paginação para quando uma página vem com menos feições do que o
    /// tamanho pedido, que é o sinal de que acabou. Contar pelo .shx é exato e
    /// custa um <c>stat</c>: o índice do shapefile tem cabeçalho de 100 bytes e
    /// oito bytes por registro.
    /// </remarks>
    private async Task<IReadOnlyList<string>> BaixarTudoAsync(
        FonteDeCamada fonte, string pasta, CancellationToken cancellationToken)
    {
        if (!fonte.EhPaginada)
        {
            var unico = await BaixarAsync(fonte, pasta, cancellationToken);
            return new[] { await PrepararArquivoAsync(fonte, unico, pasta, cancellationToken) };
        }

        // Teto de páginas: uma origem que ignorasse o startIndex devolveria
        // sempre a primeira página cheia, e o laço não terminaria nunca.
        const int MaximoDePaginas = 200;

        var arquivos = new List<string>();

        for (var pagina = 0; pagina < MaximoDePaginas; pagina++)
        {
            var inicio = pagina * CatalogoDeFontes.PorPagina;
            var daPagina = Path.Combine(pasta, $"p{pagina:D3}");
            Directory.CreateDirectory(daPagina);

            var baixado = await BaixarAsync(fonte, daPagina, cancellationToken, inicio);
            var arquivo = await PrepararArquivoAsync(fonte, baixado, daPagina, cancellationToken);

            var naPagina = FeicoesNoShapefile(arquivo);

            if (naPagina > 0)
            {
                arquivos.Add(arquivo);
            }

            _logger.LogInformation(
                "{Chave}: página {Pagina} trouxe {Feicoes:N0} feições (a partir de {Inicio:N0}).",
                fonte.Chave, pagina + 1, naPagina, inicio);

            if (naPagina < CatalogoDeFontes.PorPagina)
            {
                break;
            }
        }

        return arquivos;
    }

    /// <summary>
    /// Quantas feições a origem diz ter, ou nulo quando ela não sabe responder.
    /// </summary>
    /// <remarks>
    /// Esta é a conferência que faltava, e a lição mais cara desta base. O
    /// GeoServer do INPE limita cada requisição a 50.000 feições e, quando o
    /// pedido passa disso, devolve as primeiras 50.000 com <b>200 OK</b>. Nada
    /// falha: o zip é válido, o shapefile abre, o importador grava, a troca
    /// versionada publica. O PRODES da Amazônia entrou no ar com 50.000
    /// polígonos de 802.277 — 6% — e nenhum registro em lugar nenhum disse isso.
    ///
    /// A paginação resolve o caso conhecido. Esta contagem resolve o
    /// desconhecido: qualquer origem que passe a entregar menos do que anuncia
    /// — teto novo, filtro que mudou de significado, resposta cortada — é
    /// recusada em vez de publicada pela metade.
    ///
    /// Falhar aqui não derruba a carga. Nem toda origem é WFS, nem todo WFS
    /// aceita <c>resultType=hits</c>, e trocar uma recarga boa por nenhuma
    /// porque a conferência não pôde ser feita seria o remédio pior.
    /// </remarks>
    private async Task<int?> ContarNaOrigemAsync(
        FonteDeCamada fonte, CancellationToken cancellationToken)
    {
        var consulta = ConsultaDeContagem(fonte.UrlResolvida());

        if (consulta is null)
        {
            return null;
        }

        try
        {
            using var resposta = await Http.GetAsync(consulta, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                return SemConferencia(fonte, $"a origem respondeu {(int)resposta.StatusCode}");
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            // numberMatched no WFS 2.0.0; numberOfFeatures nas versões antigas.
            var achado = System.Text.RegularExpressions.Regex.Match(
                corpo, @"number(?:Matched|OfFeatures)=""(\d+)""");

            if (!achado.Success || !int.TryParse(achado.Groups[1].Value, out var total))
            {
                return SemConferencia(fonte, "a resposta não traz a contagem");
            }

            _logger.LogInformation("{Chave}: a origem declara {Total:N0} feições.", fonte.Chave, total);

            return total;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SemConferencia(fonte, ex.Message);
        }
    }

    /// <summary>
    /// Registra que esta carga entrou sem ser conferida contra a origem.
    /// </summary>
    /// <remarks>
    /// Aviso, e não silêncio. A primeira versão desta sonda tinha um erro de
    /// uma letra na expressão que lê a contagem: ela nunca casava, devolvia
    /// nulo, e a carga seguia sem conferência alguma. Como nada falhava, só
    /// apareceu porque alguém estava lendo o registro na hora.
    ///
    /// Uma conferência que pode se desligar sozinha sem avisar não é
    /// conferência — é a mesma omissão que ela existe para impedir, um nível
    /// acima.
    /// </remarks>
    private int? SemConferencia(FonteDeCamada fonte, string motivo)
    {
        _logger.LogWarning(
            "{Chave}: a carga não pôde ser conferida contra a origem ({Motivo}). Ela entra " +
            "sem essa rede de proteção — uma resposta truncada passaria despercebida.",
            fonte.Chave, motivo);

        return null;
    }

    /// <summary>
    /// Transforma a URL de download na mesma consulta pedindo só a contagem.
    /// </summary>
    /// <remarks>
    /// Mesmos <c>typeName</c> e <c>CQL_FILTER</c>, de propósito: o número só
    /// serve para conferir se for exatamente o recorte que está sendo baixado.
    /// A versão sobe para 2.0.0 porque <c>resultType=hits</c> não existe na
    /// 1.0.0 — e aqui a troca é segura, já que nenhuma geometria vem na
    /// resposta e a inversão de eixos da 1.1.0 em diante não tem o que inverter.
    /// </remarks>
    private static string? ConsultaDeContagem(string url)
    {
        var separador = url.IndexOf('?');

        if (separador < 0 || !url.Contains("service=WFS", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var partes = new List<string>();

        foreach (var par in url[(separador + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var nome = par.Split('=', 2)[0];

            if (nome.Equals("outputFormat", StringComparison.OrdinalIgnoreCase) ||
                nome.Equals("maxFeatures", StringComparison.OrdinalIgnoreCase) ||
                nome.Equals("startIndex", StringComparison.OrdinalIgnoreCase) ||
                nome.Equals("sortBy", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            partes.Add(nome.Equals("version", StringComparison.OrdinalIgnoreCase) ? "version=2.0.0"
                     : nome.Equals("typeName", StringComparison.OrdinalIgnoreCase)
                         ? "typeNames=" + par.Split('=', 2)[1]
                         : par);
        }

        partes.Add("resultType=hits");

        return url[..separador] + "?" + string.Join("&", partes);
    }

    /// <summary>Feições de um shapefile, lidas do índice sem abrir a geometria.</summary>
    private static int FeicoesNoShapefile(string caminhoShp)
    {
        var shx = Path.ChangeExtension(caminhoShp, ".shx");

        if (!File.Exists(shx))
        {
            return 0;
        }

        return (int)Math.Max(0, (new FileInfo(shx).Length - 100) / 8);
    }

    private async Task<string> BaixarAsync(
        FonteDeCamada fonte, string pasta, CancellationToken cancellationToken, int? inicio = null)
    {
        var endereco = fonte.UrlResolvida();

        if (inicio is int desde)
        {
            endereco +=
                $"&sortBy={Uri.EscapeDataString(fonte.ChaveDeOrdenacao!)}" +
                $"&startIndex={desde}&maxFeatures={CatalogoDeFontes.PorPagina}";
        }

        using var requisicao = new HttpRequestMessage(
            fonte.EhPost ? HttpMethod.Post : HttpMethod.Get, endereco);

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
        FonteDeCamada fonte,
        IReadOnlyList<string> arquivos,
        int? esperado,
        CancellationToken cancellationToken)
    {
        if (arquivos.Count == 0)
        {
            throw new InvalidOperationException("A origem não devolveu feição alguma.");
        }

        switch (fonte.Formato)
        {
            case FormatoDaFonte.CsvEmbargo:
            {
                var resultado = await _embargo.ImportarAsync(arquivos[0], cancellationToken);

                return $"{Formatos.Quantidade(resultado.Gravados)} polígonos e " +
                       $"{Formatos.Quantidade(resultado.Documentos)} registros por documento.";
            }

            case FormatoDaFonte.GeoJsonEmZip:
            {
                var resultado = await _geoJson.ImportarAsync(
                    arquivos[0], fonte.Chave, fonte.Nome, fonte.Tipo, fonte.Origem,
                    fonte.AnoReferencia, tenantId: null, fonte.Biomas, esperado, cancellationToken);

                return Descrever(resultado);
            }

            default:
            {
                var resultado = await _shapefile.ImportarAsync(
                    arquivos, fonte.Chave, fonte.Nome, fonte.Tipo, fonte.Origem,
                    fonte.AnoReferencia, tenantId: null, fonte.Biomas, esperado, cancellationToken);

                return Descrever(resultado);
            }
        }
    }

    private static string Descrever(ResultadoImportacaoCamada resultado) =>
        $"{Formatos.Quantidade(resultado.Gravados)} feições " +
        $"({Formatos.Quantidade(resultado.Descartados)} descartadas).";

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
