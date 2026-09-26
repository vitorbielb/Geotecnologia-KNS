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
    .AddLogging(builder => builder.AddSimpleConsole(options => options.SingleLine = true))
    .AddGeo(connectionString)
    .BuildServiceProvider();

using var scope = services.CreateScope();

try
{
    switch (comando)
    {
        case "migrar":
            await scope.ServiceProvider.GetRequiredService<GeoDbContext>().Database.MigrateAsync();
            Console.WriteLine("Esquema geo atualizado.");
            return 0;

        case "importar":
            return await ImportarAsync(scope.ServiceProvider, configuration);

        case "consultar":
            return await ConsultarAsync(scope.ServiceProvider, configuration);

        default:
            Console.Error.WriteLine("Comandos:");
            Console.Error.WriteLine("  inspecionar --arquivo <caminho.shp>");
            Console.Error.WriteLine("  migrar");
            Console.Error.WriteLine("  importar --arquivo <caminho.shp> --origem <nome> [--uf UF]");
            Console.Error.WriteLine("  consultar --car <codigo>");
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Falhou: {ex.Message}");
    return 1;
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
