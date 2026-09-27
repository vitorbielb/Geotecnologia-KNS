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

        case "camadas":
            return await ListarCamadasAsync(scope.ServiceProvider);

        case "cruzar":
            return await CruzarAsync(scope.ServiceProvider, configuration);

        default:
            Console.Error.WriteLine("Comandos:");
            Console.Error.WriteLine("  diagnostico");
            Console.Error.WriteLine("  inspecionar --arquivo <caminho.shp>");
            Console.Error.WriteLine("  migrar");
            Console.Error.WriteLine("  importar --arquivo <caminho.shp> --origem <nome> [--uf UF]");
            Console.Error.WriteLine("  consultar --car <codigo>");
            Console.Error.WriteLine("  importar-camada --arquivo <caminho.shp> --chave <chave> --nome <nome>");
            Console.Error.WriteLine("                  --tipo <tipo> --origem <origem> [--ano <ano>]");
            Console.Error.WriteLine("  camadas");
            Console.Error.WriteLine("  cruzar --car <codigo>");
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
