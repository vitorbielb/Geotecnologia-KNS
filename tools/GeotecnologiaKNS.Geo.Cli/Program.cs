using System.Globalization;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo;
using GeotecnologiaKNS.Geo.Ingestao;
using GeotecnologiaKNS.Geo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Ferramenta de linha de comando para carregar a base pública do CAR no PostGIS.
//
// O download do shapefile é manual: o portal do SICAR protege o download com
// CAPTCHA e não publica WFS nem API de download em massa. Baixe a camada
// nacional do MapBiomas ("CAR - Camada Completa") ou os arquivos por município
// do SICAR e aponte esta ferramenta para o .shp resultante.
//
//   dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- \
//       importar --arquivo "C:\bases\AREA_IMOVEL.shp" \
//                --origem "MapBiomas/CAR-Camada-Completa" --uf MT
//
//   dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- migrar
//   dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- consultar --car MT-5107925-XXXX

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// A ferramenta roda em servidor, às vezes Linux, onde a cultura do processo é a
// invariante — e aí "57.843 feições" sairia como "57,843 feições" para um
// operador brasileiro. Fixada aqui, uma vez, para toda a saída.
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

var comando = args.FirstOrDefault()?.ToLowerInvariant();

// Inspecionar lê só o arquivo; não faz sentido exigir banco para isso.
if (comando == "inspecionar")
{
    try
    {
        return Inspecionar(configuration);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Falhou: {ex.Message}");
        return 1;
    }
}

var connectionString = configuration.GetConnectionString("Geo")
    ?? configuration["ConnectionStrings__Geo"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Defina a connection string em ConnectionStrings:Geo (appsettings.json ou variável de ambiente).");
    return 1;
}

var services = new ServiceCollection()
    .AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true)
        .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning))
    .AddGeo(connectionString)
    .BuildServiceProvider();

using var scope = services.CreateScope();

try
{
    switch (comando)
    {
        case "diagnostico":
            return await DiagnosticarAsync(scope.ServiceProvider, connectionString);

        case "migrar":
            await scope.ServiceProvider.GetRequiredService<GeoDbContext>().Database.MigrateAsync();
            Console.WriteLine("Esquema geo atualizado.");
            return 0;

        case "importar":
            return await ImportarAsync(scope.ServiceProvider, configuration);

        case "consultar":
            return await ConsultarAsync(scope.ServiceProvider, configuration);

        case "importar-camada":
            return await ImportarCamadaAsync(scope.ServiceProvider, configuration);

        case "importar-cadastro-empregadores":
            return await ImportarCadastroAsync(scope.ServiceProvider, configuration);

        case "importar-geojson":
            return await ImportarGeoJsonAsync(scope.ServiceProvider, configuration);

        case "importar-embargo":
            return await ImportarEmbargoAsync(scope.ServiceProvider, configuration);

        case "camadas":
            return await ListarCamadasAsync(scope.ServiceProvider);

        case "recarregar":
            return await RecarregarAsync(scope.ServiceProvider, configuration, args);

        case "cruzar":
            return await CruzarAsync(scope.ServiceProvider, configuration);

        case "simular-car":
            return await SimularCarAsync(scope.ServiceProvider, configuration);

        case "cobertura":
            return await CoberturaAsync(scope.ServiceProvider);

        case "imoveis":
            return await ListarImoveisAsync(scope.ServiceProvider);

        case "importar-perimetro":
            return await ImportarPerimetroAsync(scope.ServiceProvider, configuration);

        default:
            Console.Error.WriteLine("Comandos:");
            Console.Error.WriteLine("  diagnostico");
            Console.Error.WriteLine("  inspecionar --arquivo <caminho.shp>");
            Console.Error.WriteLine("  migrar");
            Console.Error.WriteLine("  importar --arquivo <caminho.shp> --origem <nome> [--uf UF]");
            Console.Error.WriteLine("  consultar --car <codigo>");
            Console.Error.WriteLine("  importar-camada --arquivo <caminho.shp> --chave <chave> --nome <nome>");
            Console.Error.WriteLine("                  --tipo <tipo> --origem <origem> [--ano <ano>]");
            Console.Error.WriteLine("  importar-geojson --arquivo <arq.geojson> --chave <chave> --nome <nome>");
            Console.Error.WriteLine("                   --tipo <tipo> --origem <origem> [--ano <ano>]");
            Console.Error.WriteLine("  importar-cadastro-empregadores --arquivo <cadastro.txt>");
            Console.Error.WriteLine("  importar-embargo --arquivo <termo_de_embargo.csv>");
            Console.Error.WriteLine("  camadas");
            Console.Error.WriteLine("  recarregar [--chave <chave>] [--todas]");
            Console.Error.WriteLine("  cruzar --car <codigo> [--tenant <id>]");
            Console.Error.WriteLine("  simular-car --car <codigo> [--area-ha 1000]");
            Console.Error.WriteLine("  cobertura");
            Console.Error.WriteLine("  imoveis");
            Console.Error.WriteLine("  importar-perimetro --arquivo <arq.zip|.geojson> --nome <nome> --tenant <id>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Tipos de camada: " + string.Join(", ", Enum.GetNames<TipoCamada>()));
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Falhou: {ex.Message}");
    return 1;
}

/// <summary>
/// Mostra o que está coberto e, principalmente, o que os clientes pediram e não
/// existe. A fila de lacunas é o que guia a próxima carga: em vez de tentar
/// mapear o país, carrega-se o que está sendo usado de verdade.
/// </summary>
static async Task<int> CoberturaAsync(IServiceProvider provider)
{
    var contexto = provider.GetRequiredService<GeoDbContext>();

    var cobertos = await contexto.Cobertura
        .OrderBy(x => x.Uf).ThenBy(x => x.Municipio)
        .ToListAsync();

    Console.WriteLine($"Municípios cobertos: {cobertos.Count}");

    if (cobertos.Count > 0)
    {
        var porUf = cobertos
            .GroupBy(x => x.Uf)
            .Select(g => new { Uf = g.Key, Municipios = g.Count(), Imoveis = g.Sum(x => x.Imoveis) })
            .OrderByDescending(x => x.Municipios);

        Console.WriteLine();
        Console.WriteLine($"  {"UF",-4} {"MUNICÍPIOS",11} {"IMÓVEIS",12}  MAIS ANTIGA");

        foreach (var uf in porUf)
        {
            var maisAntiga = cobertos.Where(x => x.Uf == uf.Uf).Min(x => x.CobertoEm);
            Console.WriteLine($"  {uf.Uf,-4} {uf.Municipios,11} {uf.Imoveis,12:N0}  {maisAntiga:dd/MM/yyyy}");
        }
    }

    var lacunas = await contexto.Lacunas.ToListAsync();

    Console.WriteLine();

    if (lacunas.Count == 0)
    {
        Console.WriteLine("Nenhum município pendente: tudo que foi consultado está coberto.");
        return 0;
    }

    // Ordenado por quantos clientes distintos pedem, não por número de
    // consultas: município pedido por três indústrias vale mais que um
    // município que uma só consultou trinta vezes.
    var fila = lacunas
        .GroupBy(x => new { x.CodigoIbge, x.Uf })
        .Select(g => new
        {
            g.Key.CodigoIbge,
            g.Key.Uf,
            Industrias = g.Select(x => x.TenantId).Distinct().Count(),
            Consultas = g.Sum(x => x.Consultas),
            Ultima = g.Max(x => x.UltimaEm),
            Exemplo = g.OrderByDescending(x => x.UltimaEm).First().UltimoCodigoCar
        })
        .OrderByDescending(x => x.Industrias)
        .ThenByDescending(x => x.Consultas)
        .ToList();

    Console.WriteLine($"Municípios pedidos e não carregados: {fila.Count}");
    Console.WriteLine();
    Console.WriteLine($"  {"UF",-4} {"IBGE",-9} {"INDÚSTRIAS",11} {"CONSULTAS",10}  ÚLTIMO PEDIDO");

    foreach (var item in fila)
    {
        Console.WriteLine($"  {item.Uf,-4} {item.CodigoIbge,-9} {item.Industrias,11} {item.Consultas,10}  {item.Ultima:dd/MM/yyyy}");
    }

    Console.WriteLine();
    Console.WriteLine("Carregue estes municípios pelo portal do SICAR e importe com 'importar'.");
    Console.WriteLine("A importação resolve as lacunas correspondentes automaticamente.");

    return 0;
}

/// <summary>
/// Marca usada em toda carga simulada. É por ela que o resto do sistema
/// distingue perímetro inventado de perímetro oficial.
/// </summary>
const string OrigemSimulada = "SIMULADO - NAO USAR PARA DECISAO";

/// <summary>
/// Insere um perímetro fictício para um código de CAR, ancorado no município
/// que o próprio código indica.
/// </summary>
/// <remarks>
/// Serve para exercitar o fluxo enquanto a base real do SICAR não está
/// carregada — o download dela não é automatizável (CAPTCHA no portal do SICAR,
/// plataforma com login no MapBiomas).
///
/// O município e a localização são verdadeiros: o código do CAR carrega o
/// código IBGE, e a malha municipal vem da API do IBGE. Só o perímetro é
/// inventado — um quadrado da área pedida sobre o centroide do município.
///
/// Num sistema que veda compra, perímetro inventado sob código real é
/// perigoso: alguém pode analisar o imóvel depois sem saber da procedência.
/// Por isso a carga é marcada de forma inequívoca, a marca aparece na tela e
/// no laudo, e o comando se recusa a sobrescrever registro vindo de carga real.
/// </remarks>
static async Task<int> SimularCarAsync(IServiceProvider provider, IConfiguration configuration)
{
    var codigo = CodigoCar.Normalizar(configuration["car"]);

    if (codigo is null)
    {
        Console.Error.WriteLine("Informe --car com um código válido (UF-CódigoIBGE-Hash).");
        return 1;
    }

    var areaHa = double.TryParse(configuration["area-ha"], NumberStyles.Any, CultureInfo.InvariantCulture, out var a) && a > 0
        ? a
        : 1000d;

    var contexto = provider.GetRequiredService<GeoDbContext>();
    var conexao = (Npgsql.NpgsqlConnection)contexto.Database.GetDbConnection();

    if (conexao.State != System.Data.ConnectionState.Open)
    {
        await conexao.OpenAsync();
    }

    // Não sobrescreve dado oficial: se o CAR já veio de uma carga real, a
    // simulação degradaria a base sem deixar rastro.
    await using (var checagem = conexao.CreateCommand())
    {
        checagem.CommandText = @"
            SELECT c.origem
            FROM geo.imovel_car i
            JOIN geo.carga_base_car c ON c.id = i.carga_id
            WHERE i.codigo_car = @codigo";
        checagem.Parameters.AddWithValue("codigo", codigo);

        if (await checagem.ExecuteScalarAsync() is string origemAtual && origemAtual != OrigemSimulada)
        {
            Console.Error.WriteLine($"{codigo} já existe na base, vindo de '{origemAtual}'.");
            Console.Error.WriteLine("Simular por cima apagaria o perímetro real. Nada foi alterado.");
            return 1;
        }
    }

    var codigoIbge = CodigoCar.ExtrairCodigoIbge(codigo)!;
    Console.WriteLine($"CAR      : {codigo}");
    Console.WriteLine($"Município: código IBGE {codigoIbge}");

    string municipio, uf, malhaGeoJson;

    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
    {
        try
        {
            var meta = await http.GetStringAsync(
                $"https://servicodados.ibge.gov.br/api/v1/localidades/municipios/{codigoIbge}");

            using var doc = System.Text.Json.JsonDocument.Parse(meta);
            municipio = doc.RootElement.GetProperty("nome").GetString() ?? "(desconhecido)";
            uf = doc.RootElement.GetProperty("microrregiao").GetProperty("mesorregiao")
                    .GetProperty("UF").GetProperty("sigla").GetString() ?? CodigoCar.ExtrairUf(codigo)!;

            var malha = await http.GetStringAsync(
                $"https://servicodados.ibge.gov.br/api/v3/malhas/municipios/{codigoIbge}" +
                "?formato=application/vnd.geo+json&qualidade=minima");

            using var geo = System.Text.Json.JsonDocument.Parse(malha);
            malhaGeoJson = geo.RootElement.GetProperty("features")[0].GetProperty("geometry").GetRawText();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Não foi possível consultar o IBGE: {CausaRaiz(ex)}");
            return 1;
        }
    }

    Console.WriteLine($"           {municipio}/{uf}");

    // Quadrado equivalente à área pedida: metade do lado como raio do buffer.
    var ladoMetros = Math.Sqrt(areaHa * 10_000);

    await using var comando = conexao.CreateCommand();
    comando.CommandText = @"
        WITH carga AS (
            INSERT INTO geo.carga_base_car
                (origem, arquivo, uf, iniciada_em, concluida_em,
                 registros_lidos, registros_gravados, registros_descartados, status)
            VALUES (@origem, 'simular-car', @uf, now(), now(), 1, 1, 0, 1)
            RETURNING id
        ), municipio AS (
            SELECT ST_SetSRID(ST_GeomFromGeoJSON(@malha), 4326) AS geom
        )
        INSERT INTO geo.imovel_car
            (codigo_car, perimetro, centroide, area_ha, municipio, uf, codigo_ibge, situacao, tipo, carga_id)
        SELECT @codigo,
               ST_SetSRID(ST_Envelope(ST_Buffer(ST_Centroid(m.geom)::geography, @raio)::geometry), 4326),
               ST_SetSRID(ST_Centroid(m.geom), 4326),
               @area, @municipio, @uf, @ibge, 'AT', 'IRU', carga.id
        FROM municipio m, carga
        ON CONFLICT (codigo_car) DO UPDATE
           SET perimetro = EXCLUDED.perimetro,
               centroide = EXCLUDED.centroide,
               area_ha   = EXCLUDED.area_ha,
               municipio = EXCLUDED.municipio,
               uf        = EXCLUDED.uf,
               carga_id  = EXCLUDED.carga_id;";

    comando.Parameters.AddWithValue("origem", OrigemSimulada);
    comando.Parameters.AddWithValue("codigo", codigo);
    comando.Parameters.AddWithValue("malha", malhaGeoJson);
    comando.Parameters.AddWithValue("raio", ladoMetros / 2);
    comando.Parameters.AddWithValue("area", areaHa);
    comando.Parameters.AddWithValue("municipio", municipio);
    comando.Parameters.AddWithValue("uf", uf);
    comando.Parameters.AddWithValue("ibge", codigoIbge);

    await comando.ExecuteNonQueryAsync();

    Console.WriteLine($"Perímetro: quadrado de {areaHa:N0} ha no centroide do município");
    Console.WriteLine($"Procedência: {OrigemSimulada}");
    Console.WriteLine();
    Console.WriteLine("Pronto. O imóvel já aparece na consulta por CAR.");
    Console.WriteLine("Atenção: o perímetro é inventado. Serve para exercitar o fluxo,");
    Console.WriteLine("não para decidir sobre o imóvel. Carregar a base real o substitui.");

    return 0;
}

/// <summary>
/// Percorre a cadeia toda na ordem em que ela quebra e diz qual elo falta.
/// Existe porque "a base do CAR não foi carregada" tem várias causas possíveis
/// — sem servidor, sem esquema, sem carga — e cada uma tem um remédio.
/// </summary>
static async Task<int> DiagnosticarAsync(IServiceProvider provider, string connectionString)
{
    var problemas = 0;
    var contexto = provider.GetRequiredService<GeoDbContext>();

    Console.WriteLine("Diagnóstico da base geoespacial");
    Console.WriteLine(new string('-', 52));

    var servidor = "(não identificado)";
    try
    {
        var construtor = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        servidor = $"{construtor.Host}:{construtor.Port}/{construtor.Database}";
    }
    catch { /* connection string malformada; o teste de conexão abaixo acusa */ }

    Console.WriteLine($"Servidor configurado : {servidor}");

    // 1. Conexão
    try
    {
        await contexto.Database.OpenConnectionAsync();
        await contexto.Database.CloseConnectionAsync();
        Console.WriteLine("Conexão             : OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Conexão             : FALHOU");
        Console.WriteLine($"  {CausaRaiz(ex)}");
        Console.WriteLine();
        Console.WriteLine("  O PostgreSQL não está no ar ou a connection string está errada.");
        Console.WriteLine("  Com Docker:  docker compose up -d postgis");
        return 1;
    }

    // 2. Esquema
    try
    {
        var pendentes = (await contexto.Database.GetPendingMigrationsAsync()).ToList();

        if (pendentes.Count == 0)
        {
            Console.WriteLine("Esquema             : atualizado");
        }
        else
        {
            problemas++;
            Console.WriteLine($"Esquema             : {pendentes.Count} migration(s) pendente(s)");
            Console.WriteLine("  Execute:  dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- migrar");
        }
    }
    catch (Exception ex)
    {
        problemas++;
        Console.WriteLine("Esquema             : não pôde ser verificado");
        Console.WriteLine($"  {CausaRaiz(ex)}");
        return 1;
    }

    // 3. Base do CAR
    try
    {
        var cargas = await contexto.Cargas.CountAsync(x => x.Status == StatusCarga.Concluida);
        var imoveis = await contexto.ImoveisCar.CountAsync();

        if (cargas == 0)
        {
            problemas++;
            Console.WriteLine("Base do CAR         : VAZIA — nenhuma carga concluída");
            Console.WriteLine("  Baixe a camada de imóveis e execute:");
            Console.WriteLine("    ... -- importar --arquivo <AREA_IMOVEL.shp> --origem <nome da fonte>");
        }
        else
        {
            Console.WriteLine($"Base do CAR         : {imoveis:N0} imóveis em {cargas} carga(s)");
        }
    }
    catch (Exception ex)
    {
        problemas++;
        Console.WriteLine("Base do CAR         : não pôde ser consultada");
        Console.WriteLine($"  {CausaRaiz(ex)}");
    }

    // 4. Camadas de cruzamento
    try
    {
        var camadas = await contexto.Camadas.CountAsync(x => x.Ativa);

        if (camadas == 0)
        {
            Console.WriteLine("Camadas de análise  : nenhuma — o cadastro funciona, a análise não acusa nada");
            Console.WriteLine("    ... -- importar-camada --arquivo <camada.shp> --chave <chave> ...");
        }
        else
        {
            Console.WriteLine($"Camadas de análise  : {camadas} ativa(s)");
        }
    }
    catch
    {
        Console.WriteLine("Camadas de análise  : não puderam ser consultadas");
    }

    // 5. Sobras de carga não publicada
    //
    // A troca versionada grava a carga nova ao lado da que está no ar. Se o
    // processo morrer no meio, aquelas linhas ficam ocupando espaço sem nunca
    // entrar em análise alguma. A próxima carga as limpa sozinha — mas enquanto
    // isso não acontece, é melhor que apareçam aqui do que crescerem em
    // silêncio num banco que ninguém olha.
    try
    {
        var feicoesPendentes = await contexto.Feicoes
            .Join(contexto.Camadas, f => f.CamadaId, c => c.Id, (f, c) => new { f.Versao, c.VersaoAtual })
            .CountAsync(x => x.Versao != x.VersaoAtual);

        var documentosPendentes = await contexto.RestricoesPorDocumento
            .Join(contexto.ListasRestritivas, r => r.Tipo, l => l.Tipo, (r, l) => new { r.Versao, l.VersaoAtual })
            .CountAsync(x => x.Versao != x.VersaoAtual);

        if (feicoesPendentes == 0 && documentosPendentes == 0)
        {
            Console.WriteLine("Cargas pendentes    : nenhuma");
        }
        else
        {
            Console.WriteLine(
                $"Cargas pendentes    : {feicoesPendentes:N0} feições e {documentosPendentes:N0} registros " +
                "de carga não publicada");
            Console.WriteLine("  Nada disso entra em análise. A próxima recarga da camada limpa.");
        }
    }
    catch
    {
        Console.WriteLine("Cargas pendentes    : não puderam ser consultadas");
    }

    Console.WriteLine(new string('-', 52));
    Console.WriteLine(problemas == 0
        ? "Tudo pronto para cadastrar imóveis pelo CAR."
        : $"{problemas} pendência(s) acima impedem a consulta ao CAR.");

    return problemas == 0 ? 0 : 1;
}

/// <summary>
/// A mensagem externa do Npgsql costuma ser genérica ("transient failure");
/// a causa útil ("connection refused", "password authentication failed")
/// está na exceção mais interna.
/// </summary>
static string CausaRaiz(Exception ex)
{
    var atual = ex;
    while (atual.InnerException is not null) { atual = atual.InnerException; }
    return atual.Message.Split('\n')[0].Trim();
}

static int Inspecionar(IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];

    if (string.IsNullOrWhiteSpace(arquivo))
    {
        Console.Error.WriteLine("Informe --arquivo <caminho.shp>.");
        return 1;
    }

    var inspecao = ShapefileInspector.Inspecionar(arquivo);

    Console.WriteLine($"Arquivo:   {inspecao.Arquivo}");
    Console.WriteLine($"Geometria: {inspecao.TipoGeometria ?? "(nenhuma)"}");
    Console.WriteLine($"Amostra:   {inspecao.RegistrosAmostrados} feição(ões)");
    Console.WriteLine();
    Console.WriteLine($"{"COLUNA",-16} {"RECONHECIDA COMO",-16} EXEMPLO");

    foreach (var campo in inspecao.Campos)
    {
        var destino = campo.Reconhecido ? campo.MapeadoPara! : "-";
        var exemplo = campo.Exemplo ?? string.Empty;

        if (exemplo.Length > 45)
        {
            exemplo = exemplo[..45] + "...";
        }

        Console.WriteLine($"{campo.Nome,-16} {destino,-16} {exemplo}");
    }

    if (inspecao.Problemas.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Todas as colunas necessárias foram reconhecidas. Pode importar.");
        return 0;
    }

    Console.WriteLine();

    foreach (var problema in inspecao.Problemas)
    {
        Console.WriteLine($"ATENÇÃO: {problema}");
    }

    Console.WriteLine();
    Console.WriteLine("Ajuste os nomes aceitos em SicarShapefileImporter.MapeamentoDeCampos antes de importar.");

    return 2;
}

static async Task<int> ImportarAsync(IServiceProvider provider, IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];
    var origem = configuration["origem"];

    if (string.IsNullOrWhiteSpace(arquivo) || string.IsNullOrWhiteSpace(origem))
    {
        Console.Error.WriteLine("Informe --arquivo <caminho.shp> e --origem <nome da fonte>.");
        return 1;
    }

    var importer = provider.GetRequiredService<SicarShapefileImporter>();
    var resultado = await importer.ImportarAsync(arquivo, origem, configuration["uf"]);

    Console.WriteLine($"Carga {resultado.CargaId}: {resultado.Gravados} gravados, {resultado.Descartados} descartados de {resultado.Lidos} lidos.");

    foreach (var aviso in resultado.Avisos.Take(20))
    {
        Console.WriteLine($"  aviso: {aviso}");
    }

    if (resultado.Avisos.Count > 20)
    {
        Console.WriteLine($"  ... e mais {resultado.Avisos.Count - 20} avisos.");
    }

    return 0;
}

static async Task<int> ImportarCamadaAsync(IServiceProvider provider, IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];
    var chave = configuration["chave"];
    var nome = configuration["nome"];
    var origem = configuration["origem"];
    var tipoTexto = configuration["tipo"];

    if (string.IsNullOrWhiteSpace(arquivo) || string.IsNullOrWhiteSpace(chave) ||
        string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(origem) ||
        string.IsNullOrWhiteSpace(tipoTexto))
    {
        Console.Error.WriteLine("Informe --arquivo, --chave, --nome, --tipo e --origem.");
        Console.Error.WriteLine("Tipos: " + string.Join(", ", Enum.GetNames<TipoCamada>()));
        return 1;
    }

    if (!Enum.TryParse<TipoCamada>(tipoTexto, ignoreCase: true, out var tipo))
    {
        Console.Error.WriteLine($"Tipo '{tipoTexto}' desconhecido.");
        Console.Error.WriteLine("Tipos: " + string.Join(", ", Enum.GetNames<TipoCamada>()));
        return 1;
    }

    int? ano = int.TryParse(configuration["ano"], out var anoLido) ? anoLido : null;

    var importer = provider.GetRequiredService<CamadaShapefileImporter>();
    var resultado = await importer.ImportarAsync(arquivo, chave, nome, tipo, origem, ano);

    Console.WriteLine(
        $"Camada '{resultado.Chave}' (id {resultado.CamadaId}): " +
        $"{resultado.Gravados} feições gravadas, {resultado.Descartados} descartadas de {resultado.Lidos} lidas.");

    return 0;
}

static async Task<int> ListarCamadasAsync(IServiceProvider provider)
{
    // Sincroniza antes de listar: a periodicidade mora no catálogo, e uma
    // camada sem prazo gravado apareceria como "sem prazo" só porque ninguém
    // rodou a recarga ainda.
    await provider.GetRequiredService<RecarregadorDeCamadas>().SincronizarCatalogoAsync();

    var contexto = provider.GetRequiredService<GeoDbContext>();

    var camadas = await contexto.Camadas.AsNoTracking()
        .OrderBy(x => x.Tipo).ThenBy(x => x.Nome).ToListAsync();

    if (camadas.Count == 0)
    {
        Console.WriteLine("Nenhuma camada carregada. Use 'recarregar --todas' para baixar das origens.");
        return 0;
    }

    Console.WriteLine($"{"CHAVE",-26} {"TIPO",-24} {"FEIÇÕES",9}  {"ATUALIZADA",-14} SITUAÇÃO");

    foreach (var camada in camadas.Where(x => x.TenantId is null))
    {
        Console.WriteLine(
            $"{Encurtar(camada.Chave, 26),-26} {camada.Tipo,-24} {camada.TotalFeicoes,9:N0}  " +
            $"{Quando(camada.AtualizadaEm),-14} {Situacao(camada.AtualizadaEm, camada.PeriodicidadeDias)}");
    }

    // Separadas das públicas de propósito: estas são de uma indústria só, e
    // misturá-las na mesma lista daria a impressão de que valem para todas.
    var proprias = camadas.Where(x => x.TenantId is not null).ToList();

    if (proprias.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"{"PERÍMETRO PRÓPRIO",-26} {"INDÚSTRIA",-24} {"ÁREAS",9}  {"CARREGADO",-14}");

        foreach (var camada in proprias.OrderBy(x => x.TenantId).ThenBy(x => x.Nome))
        {
            Console.WriteLine(
                $"{Encurtar(camada.Nome, 26),-26} {camada.TenantId,-24} {camada.TotalFeicoes,9:N0}  " +
                $"{Quando(camada.AtualizadaEm),-14}");
        }
    }

    var listas = await contexto.ListasRestritivas.AsNoTracking().OrderBy(x => x.Tipo).ToListAsync();

    if (listas.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"{"LISTA POR DOCUMENTO",-26} {"",-24} {"REGISTROS",9}  {"ATUALIZADA",-14} SITUAÇÃO");

        foreach (var lista in listas)
        {
            Console.WriteLine(
                $"{lista.Tipo,-26} {"",-24} {lista.TotalRegistros,9:N0}  " +
                $"{Quando(lista.AtualizadaEm),-14} {Situacao(lista.AtualizadaEm, lista.PeriodicidadeDias)}");
        }
    }

    return 0;
}

static string Encurtar(string texto, int limite) =>
    texto.Length <= limite ? texto : texto[..(limite - 1)] + "…";

static string Quando(DateTime? data)
{
    if (data is null)
    {
        return "nunca";
    }

    var dias = (int)(DateTime.UtcNow - data.Value).TotalDays;

    return dias switch
    {
        <= 0 => "hoje",
        1 => "ontem",
        _ => $"há {dias} dias"
    };
}

static string Situacao(DateTime? atualizada, int? periodicidade)
{
    if (periodicidade is null)
    {
        return "sem prazo definido";
    }

    if (atualizada is null)
    {
        return "NUNCA CARREGADA";
    }

    var dias = (int)Math.Ceiling((atualizada.Value.AddDays(periodicidade.Value) - DateTime.UtcNow).TotalDays);

    return dias < 0 ? $"VENCIDA há {-dias} dias" : $"vence em {dias} dias";
}

static async Task<int> RecarregarAsync(
    IServiceProvider provider, IConfiguration configuration, string[] argumentos)
{
    var recarregador = provider.GetRequiredService<RecarregadorDeCamadas>();
    await recarregador.SincronizarCatalogoAsync();

    var chave = configuration["chave"];

    // Lido de args, e não da configuração: o provedor de linha de comando do
    // .NET só reconhece "--chave valor", e descarta em silêncio uma opção
    // solta como --todas. Descobri isso com a recarga respondendo "nenhuma
    // camada vencida" a um comando que pedia todas.
    var todas = argumentos.Contains("--todas", StringComparer.OrdinalIgnoreCase);

    IReadOnlyList<FonteDeCamada> fontes;

    if (!string.IsNullOrWhiteSpace(chave))
    {
        var fonte = CatalogoDeFontes.PorChave(chave);

        if (fonte is null)
        {
            Console.Error.WriteLine($"Não conheço a camada '{chave}'. Conhecidas: " +
                string.Join(", ", CatalogoDeFontes.Todas.Select(x => x.Chave)));
            return 1;
        }

        fontes = new[] { fonte };
    }
    else if (todas)
    {
        fontes = CatalogoDeFontes.Todas;
    }
    else
    {
        fontes = await recarregador.ObterVencidasAsync();

        if (fontes.Count == 0)
        {
            Console.WriteLine("Nenhuma camada vencida. Use --todas para recarregar mesmo assim.");
            return 0;
        }
    }

    var falhas = 0;

    foreach (var fonte in fontes)
    {
        Console.WriteLine($"→ {fonte.Chave}");

        var resultado = await recarregador.RecarregarAsync(fonte);

        Console.WriteLine(resultado.Sucesso
            ? $"  ok em {resultado.Duracao.TotalSeconds:N0}s — {resultado.Mensagem}"
            : $"  FALHOU — {resultado.Mensagem}");

        if (!resultado.Sucesso)
        {
            falhas++;
        }
    }

    // O MTE vem em PDF e depende de pdftotext, então fica de fora da recarga
    // automática. Calar sobre isso daria a impressão de que a recarga cobre
    // tudo, e a lista de trabalho análogo a escravo envelheceria em silêncio.
    foreach (var lista in await recarregador.ObterListasVencidasAsync())
    {
        Console.WriteLine();
        Console.WriteLine($"! {lista.Nome} está vencida e não tem recarga automática.");
        Console.WriteLine("  Veja docs/camadas-de-referencia.md para atualizá-la.");
    }

    // Saída diferente de zero para o agendador do sistema enxergar a falha; sem
    // isso, uma recarga que não aconteceu passa por recarga bem-sucedida.
    return falhas == 0 ? 0 : 1;
}

/// <summary>
/// Carrega um perímetro próprio em nome de uma indústria.
/// </summary>
/// <remarks>
/// O caminho normal é a tela, que já sabe de quem é a sessão. Aqui o tenant é
/// obrigatório e explícito: carregar perímetro sem dono o tornaria público, e
/// público significa visível para as concorrentes da indústria que o enviou.
/// </remarks>
static async Task<int> ImportarPerimetroAsync(IServiceProvider provider, IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];
    var nome = configuration["nome"];

    if (string.IsNullOrWhiteSpace(arquivo) || string.IsNullOrWhiteSpace(nome)
        || !int.TryParse(configuration["tenant"], out var tenant) || tenant <= 0)
    {
        Console.Error.WriteLine("Informe --arquivo <caminho>, --nome <nome> e --tenant <id>.");
        return 1;
    }

    var resultado = await provider.GetRequiredService<PerimetroProprioService>()
        .ImportarAsync(arquivo, nome, tenant);

    Console.WriteLine(
        $"Perímetro \"{resultado.Nome}\" (indústria {tenant}): " +
        $"{resultado.Feicoes:N0} área(s), {resultado.Descartados:N0} descartada(s).");

    return 0;
}

/// <summary>Imóveis carregados, com a caixa envolvente — útil para testar o cruzamento.</summary>
static async Task<int> ListarImoveisAsync(IServiceProvider provider)
{
    var contexto = provider.GetRequiredService<GeoDbContext>();
    var conexao = (Npgsql.NpgsqlConnection)contexto.Database.GetDbConnection();

    if (conexao.State != System.Data.ConnectionState.Open)
    {
        await conexao.OpenAsync();
    }

    await using var comando = conexao.CreateCommand();
    comando.CommandText = @"
        SELECT codigo_car, municipio, uf,
               ST_XMin(perimetro), ST_YMin(perimetro), ST_XMax(perimetro), ST_YMax(perimetro)
        FROM geo.imovel_car ORDER BY codigo_car";

    await using var leitor = await comando.ExecuteReaderAsync();

    while (await leitor.ReadAsync())
    {
        Console.WriteLine(
            $"{leitor.GetString(0)}  {leitor.GetString(1)}/{leitor.GetString(2)}  " +
            $"[{leitor.GetDouble(3):F4} {leitor.GetDouble(4):F4} {leitor.GetDouble(5):F4} {leitor.GetDouble(6):F4}]");
    }

    return 0;
}

static async Task<int> CruzarAsync(IServiceProvider provider, IConfiguration configuration)
{
    var car = configuration["car"];

    if (string.IsNullOrWhiteSpace(car))
    {
        Console.Error.WriteLine("Informe --car <codigo do CAR>.");
        return 1;
    }

    // Sem --tenant a ferramenta não age em nome de indústria alguma e enxerga
    // só as camadas públicas. Com ele, dá para conferir da linha de comando o
    // que uma indústria específica veria — inclusive os perímetros dela.
    var tenant = int.TryParse(configuration["tenant"], out var t) ? t : (int?)null;

    var resultado = await provider.GetRequiredService<IIntersecaoService>()
        .CruzarPorCarAsync(car, tenant);

    Console.WriteLine($"CAR:   {resultado.CodigoCar}");
    Console.WriteLine($"Visão: {(tenant.HasValue ? $"indústria {tenant}" : "só camadas públicas")}");
    Console.WriteLine($"Área:  {resultado.AreaImovelHa:N2} ha");
    Console.WriteLine($"Sobreposições: {resultado.Sobreposicoes.Count}");
    Console.WriteLine();

    foreach (var sobreposicao in resultado.Sobreposicoes)
    {
        Console.WriteLine(
            $"  [{sobreposicao.Tipo}] {sobreposicao.CamadaNome}" +
            (string.IsNullOrWhiteSpace(sobreposicao.Rotulo) ? string.Empty : $" — {sobreposicao.Rotulo}"));
        Console.WriteLine(
            $"     {sobreposicao.AreaSobrepostaHa:N2} ha ({sobreposicao.PercentualDoImovel:N2}% do imóvel)");
    }

    return 0;
}

static async Task<int> ConsultarAsync(IServiceProvider provider, IConfiguration configuration)
{
    var car = configuration["car"];

    if (string.IsNullOrWhiteSpace(car))
    {
        Console.Error.WriteLine("Informe --car <codigo do CAR>.");
        return 1;
    }

    var imovel = await provider.GetRequiredService<ICarLookupService>().ObterPorCodigoAsync(car);

    if (imovel is null)
    {
        Console.WriteLine("Não encontrado na base carregada.");
        return 2;
    }

    Console.WriteLine($"CAR:       {imovel.CodigoCar}");
    Console.WriteLine($"Município: {imovel.Municipio} / {imovel.Uf}");
    Console.WriteLine($"Área:      {imovel.AreaHa} ha (declarada)");
    Console.WriteLine($"Situação:  {imovel.Situacao}");
    Console.WriteLine($"Centro:    {imovel.CentroLat}, {imovel.CentroLng}");
    Console.WriteLine($"Origem:    {imovel.Origem} (carga de {imovel.BaseCarregadaEm:yyyy-MM-dd})");

    return 0;
}

static async Task<int> ImportarEmbargoAsync(IServiceProvider provider, IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];

    if (string.IsNullOrWhiteSpace(arquivo))
    {
        Console.Error.WriteLine("Informe --arquivo com o CSV de termos de embargo do IBAMA.");
        Console.Error.WriteLine("Baixe em: https://dadosabertos.ibama.gov.br/dataset/fiscalizacao-termo-de-embargo");
        Console.Error.WriteLine("Recurso: 'Termos de embargo' (termo_de_embargo.csv)");
        return 1;
    }

    var importer = provider.GetRequiredService<EmbargoIbamaImporter>();
    var relogio = System.Diagnostics.Stopwatch.StartNew();

    var resultado = await importer.ImportarAsync(arquivo);

    relogio.Stop();

    Console.WriteLine($"Camada de embargos (id {resultado.CamadaId}):");
    Console.WriteLine($"  Lidos          : {resultado.Lidos:N0}");
    Console.WriteLine($"  Gravados       : {resultado.Gravados:N0}");
    Console.WriteLine($"  Cancelados     : {resultado.Cancelados:N0}  (embargo desfeito pelo IBAMA)");
    Console.WriteLine($"  Sem geometria  : {resultado.SemGeometria:N0}  (termo sem area delimitada)");
    Console.WriteLine($"  Invalidos      : {resultado.Invalidos:N0}  (WKT que nao pode ser lido)");
    Console.WriteLine($"  Documentos     : {resultado.Documentos:N0}  (lista restritiva por CPF/CNPJ)");
    Console.WriteLine($"  Tempo          : {relogio.Elapsed.TotalSeconds:N1}s");

    return 0;
}

static async Task<int> ImportarGeoJsonAsync(IServiceProvider provider, IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];
    var chave = configuration["chave"];
    var nome = configuration["nome"];
    var origem = configuration["origem"];
    var tipoTexto = configuration["tipo"];

    if (string.IsNullOrWhiteSpace(arquivo) || string.IsNullOrWhiteSpace(chave) ||
        string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(origem) ||
        string.IsNullOrWhiteSpace(tipoTexto))
    {
        Console.Error.WriteLine("Informe --arquivo, --chave, --nome, --tipo e --origem.");
        Console.Error.WriteLine("Tipos: " + string.Join(", ", Enum.GetNames<TipoCamada>()));
        return 1;
    }

    if (!Enum.TryParse<TipoCamada>(tipoTexto, ignoreCase: true, out var tipo))
    {
        Console.Error.WriteLine($"Tipo '{tipoTexto}' desconhecido.");
        return 1;
    }

    int? ano = int.TryParse(configuration["ano"], out var anoLido) ? anoLido : null;

    var importer = provider.GetRequiredService<CamadaGeoJsonImporter>();
    var relogio = System.Diagnostics.Stopwatch.StartNew();

    var resultado = await importer.ImportarAsync(arquivo, chave, nome, tipo, origem, ano);

    relogio.Stop();

    Console.WriteLine(
        $"Camada '{resultado.Chave}' (id {resultado.CamadaId}): " +
        $"{resultado.Gravados:N0} feicoes gravadas, {resultado.Descartados:N0} descartadas " +
        $"de {resultado.Lidos:N0} lidas em {relogio.Elapsed.TotalSeconds:N1}s.");

    return 0;
}

static async Task<int> ImportarCadastroAsync(IServiceProvider provider, IConfiguration configuration)
{
    var arquivo = configuration["arquivo"];

    if (string.IsNullOrWhiteSpace(arquivo))
    {
        Console.Error.WriteLine("Informe --arquivo com o texto extraido do Cadastro de Empregadores.");
        Console.Error.WriteLine("O MTE publica so em PDF. Converta antes:");
        Console.Error.WriteLine("  pdftotext -layout cadastro_de_empregadores.pdf cadastro.txt");
        Console.Error.WriteLine("  (o PDF vem em Latin1; converta o texto para UTF-8 antes de importar)");
        return 1;
    }

    var importer = provider.GetRequiredService<CadastroEmpregadoresImporter>();
    var resultado = await importer.ImportarAsync(arquivo);

    Console.WriteLine("Cadastro de Empregadores:");
    Console.WriteLine($"  Linhas lidas    : {resultado.Linhas:N0}");
    Console.WriteLine($"  Registros       : {resultado.Registros:N0}");
    Console.WriteLine($"  Gravados        : {resultado.Gravados:N0}");
    Console.WriteLine($"  Sem documento   : {resultado.SemDocumento:N0}  (se subir, o layout do PDF mudou)");

    return 0;
}
