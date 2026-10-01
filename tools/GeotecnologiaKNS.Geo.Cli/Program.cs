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

        case "importar-geojson":
            return await ImportarGeoJsonAsync(scope.ServiceProvider, configuration);

        case "importar-embargo":
            return await ImportarEmbargoAsync(scope.ServiceProvider, configuration);

        case "camadas":
            return await ListarCamadasAsync(scope.ServiceProvider);

        case "cruzar":
            return await CruzarAsync(scope.ServiceProvider, configuration);

        case "simular-car":
            return await SimularCarAsync(scope.ServiceProvider, configuration);

        case "cobertura":
            return await CoberturaAsync(scope.ServiceProvider);

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
            Console.Error.WriteLine("  importar-embargo --arquivo <termo_de_embargo.csv>");
            Console.Error.WriteLine("  camadas");
            Console.Error.WriteLine("  cruzar --car <codigo>");
            Console.Error.WriteLine("  simular-car --car <codigo> [--area-ha 1000]");
            Console.Error.WriteLine("  cobertura");
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
    var camadas = await provider.GetRequiredService<IIntersecaoService>().ObterCamadasAtivasAsync();

    if (camadas.Count == 0)
    {
        Console.WriteLine("Nenhuma camada ativa. Use 'importar-camada' para carregar.");
        return 0;
    }

    Console.WriteLine($"{"CHAVE",-24} {"TIPO",-24} {"FEIÇÕES",8}  NOME");

    foreach (var camada in camadas)
    {
        Console.WriteLine($"{camada.Chave,-24} {camada.Tipo,-24} {camada.TotalFeicoes,8}  {camada.Nome}");
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

    var resultado = await provider.GetRequiredService<IIntersecaoService>().CruzarPorCarAsync(car);

    Console.WriteLine($"CAR:   {resultado.CodigoCar}");
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
