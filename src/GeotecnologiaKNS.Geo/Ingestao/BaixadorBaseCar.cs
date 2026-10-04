using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoDaBaixa(
    string Escopo,
    int Paginas,
    int Lidos,
    int Gravados,
    TimeSpan Duracao,
    string? Erro = null)
{
    public bool Sucesso => Erro is null;
}

/// <summary>
/// Baixa a base do CAR direto do SICAR, por estado ou por município.
/// </summary>
/// <remarks>
/// O caminho até aqui vale ser registrado, porque a conclusão anterior estava
/// errada e custou dinheiro na planilha: o download em massa do CAR parecia
/// exigir compra ou o CAPTCHA da consulta pública. Os dois portais de download
/// do car.gov.br caem na consulta protegida, e o GeoServer em
/// <c>/geoserver/ows</c> responde 200 com a lista de camadas vazia.
///
/// Mas o SICAR publica, num endereço documentado e sem proteção alguma, um WFS
/// por unidade da federação em <c>/geoserver/sicar/wfs</c>: 27 camadas
/// <c>sicar_imoveis_XX</c>, com código do CAR, perímetro, município, área e
/// condição do cadastro. É a base oficial, de graça.
///
/// O servidor devolve no máximo dez mil feições por requisição — inclusive na
/// contagem, que também sai limitada em dez mil e por isso não serve para saber
/// o tamanho real. Daí a paginação por <c>startIndex</c> com ordem fixa em
/// <c>cod_imovel</c>: sem ordenação explícita, duas páginas podem repetir um
/// imóvel e pular outro, e o buraco não apareceria em lugar nenhum.
/// </remarks>
public class BaixadorBaseCar
{
    /// <summary>Teto de feições por requisição imposto pelo servidor do SICAR.</summary>
    private const int PorPagina = 10_000;

    /// <summary>
    /// Limite de páginas por escopo, como rede de segurança.
    /// </summary>
    /// <remarks>
    /// São dez milhões de imóveis por estado no limite — ordens de grandeza
    /// acima do maior. Existe para que um defeito na paginação vire erro em vez
    /// de laço que baixa para sempre.
    /// </remarks>
    private const int MaximoDePaginas = 1_000;

    private const string Servico = "https://geoserver.car.gov.br/geoserver/sicar/wfs";

    /// <summary>
    /// Uma página grande leva segundos; um estado inteiro, minutos. O padrão de
    /// 100s do HttpClient derruba a carga no meio.
    /// </summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(20) };

    /// <summary>Prefixo do código do IBGE → sigla da UF.</summary>
    /// <remarks>
    /// Os dois primeiros dígitos do código de município identificam o estado.
    /// Precisa disso porque a camada do SICAR é por UF, e quem pede um município
    /// informa só o código dele.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> UfPorPrefixo =
        new Dictionary<string, string>
        {
            ["11"] = "RO", ["12"] = "AC", ["13"] = "AM", ["14"] = "RR", ["15"] = "PA",
            ["16"] = "AP", ["17"] = "TO", ["21"] = "MA", ["22"] = "PI", ["23"] = "CE",
            ["24"] = "RN", ["25"] = "PB", ["26"] = "PE", ["27"] = "AL", ["28"] = "SE",
            ["29"] = "BA", ["31"] = "MG", ["32"] = "ES", ["33"] = "RJ", ["35"] = "SP",
            ["41"] = "PR", ["42"] = "SC", ["43"] = "RS", ["50"] = "MS", ["51"] = "MT",
            ["52"] = "GO", ["53"] = "DF"
        };

    private readonly SicarShapefileImporter _importador;
    private readonly ILogger<BaixadorBaseCar> _logger;

    public BaixadorBaseCar(SicarShapefileImporter importador, ILogger<BaixadorBaseCar> logger)
    {
        _importador = importador;
        _logger = logger;
    }

    public static bool UfConhecida(string? uf) =>
        uf is not null && UfPorPrefixo.Values.Contains(uf.ToUpperInvariant());

    public static IReadOnlyCollection<string> Ufs => UfPorPrefixo.Values.Order().ToList();

    /// <summary>UF a que pertence um código de município do IBGE.</summary>
    public static string? UfDoMunicipio(string? codigoIbge) =>
        codigoIbge is { Length: 7 } && UfPorPrefixo.TryGetValue(codigoIbge[..2], out var uf)
            ? uf
            : null;

    /// <summary>Baixa todos os imóveis de um estado.</summary>
    public Task<ResultadoDaBaixa> BaixarUfAsync(string uf, CancellationToken cancellationToken = default)
    {
        var sigla = uf.ToUpperInvariant();

        return UfConhecida(sigla)
            // Cobre o estado pedido, e só ele: imóvel de código vizinho que
            // venha na camada é gravado, mas não faz o município dele passar
            // por carregado.
            ? BaixarAsync(
                sigla, filtro: null, escopo: sigla, cancellationToken,
                pertenceAoEscopo: codigo => UfDoMunicipio(codigo) == sigla)
            : Task.FromResult(new ResultadoDaBaixa(
                uf, 0, 0, 0, TimeSpan.Zero, $"UF desconhecida: {uf}."));
    }

    /// <summary>
    /// Baixa os imóveis de um município.
    /// </summary>
    /// <remarks>
    /// É a granularidade que casa com a demanda registrada: o sistema já anota
    /// que município alguém consultou e não estava carregado. Baixar o estado
    /// inteiro para atender a um pedido custaria minutos e dezenas de milhares
    /// de imóveis que ninguém pediu.
    /// </remarks>
    public Task<ResultadoDaBaixa> BaixarMunicipioAsync(
        string codigoIbge, CancellationToken cancellationToken = default)
    {
        var uf = UfDoMunicipio(codigoIbge);

        return uf is null
            ? Task.FromResult(new ResultadoDaBaixa(
                codigoIbge, 0, 0, 0, TimeSpan.Zero,
                $"Código do IBGE inválido: {codigoIbge}. Precisa de 7 dígitos."))
            : BaixarAsync(
                uf,
                filtro: $"cod_municipio_ibge={codigoIbge}",
                escopo: $"{uf}/{codigoIbge}",
                cancellationToken,
                pertenceAoEscopo: codigo => codigo == codigoIbge);
    }

    private async Task<ResultadoDaBaixa> BaixarAsync(
        string uf, string? filtro, string escopo, CancellationToken cancellationToken,
        Func<string, bool>? pertenceAoEscopo = null)
    {
        var relogio = Stopwatch.StartNew();
        var pasta = Path.Combine(Path.GetTempPath(), $"car-{escopo.Replace('/', '-')}-{Guid.NewGuid():N}");

        var paginas = 0;
        var lidos = 0;
        var gravados = 0;

        try
        {
            Directory.CreateDirectory(pasta);

            for (var inicio = 0; paginas < MaximoDePaginas; inicio += PorPagina)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var arquivo = await BaixarPaginaAsync(uf, filtro, inicio, pasta, cancellationToken);

                if (arquivo is null)
                {
                    break;
                }

                var resultado = await _importador.ImportarAsync(
                    arquivo, $"SICAR/WFS ({escopo})", uf, pertenceAoEscopo, cancellationToken);

                paginas++;
                lidos += resultado.Lidos;
                gravados += resultado.Gravados;

                _logger.LogInformation(
                    "CAR {Escopo}: página {Pagina} com {Lidos} imóveis ({Total} até agora).",
                    escopo, paginas, resultado.Lidos, lidos);

                // Página incompleta é a última: o servidor devolveu tudo o que
                // restava. Não existe outra forma de saber onde termina, porque
                // a contagem do SICAR também vem limitada a dez mil.
                if (resultado.Lidos < PorPagina)
                {
                    break;
                }

                Apagar(Path.GetDirectoryName(arquivo)!);
            }

            _logger.LogInformation(
                "CAR {Escopo}: {Gravados} imóveis em {Paginas} página(s), {Segundos:N0}s.",
                escopo, gravados, paginas, relogio.Elapsed.TotalSeconds);

            return new ResultadoDaBaixa(escopo, paginas, lidos, gravados, relogio.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O que já entrou fica: o importador grava por página, com upsert
            // por código do CAR. Uma queda no meio deixa a base parcial e
            // retomável, e não vazia — e a cobertura por município mostra até
            // onde chegou.
            _logger.LogError(ex, "A baixa do CAR {Escopo} parou no meio.", escopo);

            return new ResultadoDaBaixa(escopo, paginas, lidos, gravados, relogio.Elapsed, ex.Message);
        }
        finally
        {
            Apagar(pasta);
        }
    }

    /// <summary>
    /// Baixa uma página e devolve o .shp extraído, ou nulo quando acabou.
    /// </summary>
    private async Task<string?> BaixarPaginaAsync(
        string uf, string? filtro, int inicio, string pasta, CancellationToken cancellationToken)
    {
        var endereco = new List<string>
        {
            "service=WFS",

            // 1.0.0, e não 1.1.0, por um motivo que custou caro: a partir da
            // 1.1.0 o WFS honra a ordem de eixos declarada no EPSG:4674, que é
            // latitude antes de longitude, e o SHAPE-ZIP sai com as duas
            // trocadas. O GeoJSON vem certo nas duas versões, o que torna o
            // defeito ainda mais fácil de não ver.
            //
            // Nada falha quando isso acontece: os imóveis vão parar no meio do
            // Atlântico, o cruzamento não encontra nada e toda análise devolve
            // "nenhuma sobreposição". Laudo limpo para todo fornecedor é o pior
            // resultado possível, e é o que mais se parece com sucesso.
            "version=1.0.0",
            "request=GetFeature",
            $"typeName=sicar:sicar_imoveis_{uf.ToLowerInvariant()}",
            "outputFormat=SHAPE-ZIP",

            // A ordem fixa é o que torna a paginação confiável; sem ela o
            // servidor pode devolver as páginas em ordens diferentes e um
            // imóvel escapar entre duas.
            "sortBy=cod_imovel",
            $"maxFeatures={PorPagina}",
            $"startIndex={inicio}"
        };

        if (filtro is not null)
        {
            endereco.Add($"CQL_FILTER={Uri.EscapeDataString(filtro)}");
        }

        var url = $"{Servico}?{string.Join('&', endereco)}";

        using var resposta = await Http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"O SICAR respondeu {(int)resposta.StatusCode} {resposta.ReasonPhrase}.");
        }

        var daPagina = Path.Combine(pasta, inicio.ToString());
        Directory.CreateDirectory(daPagina);

        var zip = Path.Combine(daPagina, "pagina.zip");

        await using (var saida = File.Create(zip))
        await using (var entrada = await resposta.Content.ReadAsStreamAsync(cancellationToken))
        {
            await entrada.CopyToAsync(saida, cancellationToken);
        }

        var extraido = Path.Combine(daPagina, "extraido");

        try
        {
            ZipFile.ExtractToDirectory(zip, extraido);
        }
        catch (InvalidDataException)
        {
            // O GeoServer responde 200 com um XML de erro no corpo quando a
            // consulta não agrada. Sem olhar o conteúdo, isso seguiria como
            // "zip corrompido" e esconderia o que a origem explicou.
            throw new InvalidOperationException(
                $"O SICAR não devolveu um zip. O começo da resposta é: {Comeco(zip)}");
        }

        var shp = Directory.EnumerateFiles(extraido, "*.shp", SearchOption.AllDirectories)
            .OrderByDescending(x => new FileInfo(x).Length)
            .FirstOrDefault();

        // Sem .shp no zip significa resultado vazio: a página anterior já
        // tinha trazido tudo.
        return shp;
    }

    private static string Comeco(string caminho)
    {
        using var leitor = new StreamReader(caminho);
        var buffer = new char[300];
        var lidos = leitor.ReadBlock(buffer, 0, buffer.Length);

        return new string(buffer, 0, lidos).ReplaceLineEndings(" ").Trim();
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
            // Temporário preso não desfaz uma carga que deu certo.
        }
    }
}
