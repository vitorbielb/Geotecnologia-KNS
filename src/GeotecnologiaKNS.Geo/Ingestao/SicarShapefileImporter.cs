using System.Security.Cryptography;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoImportacao(
    long CargaId,
    int Lidos,
    int Gravados,
    int Descartados,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Carrega a camada AREA_IMOVEL do CAR (shapefile) para o PostGIS.
/// </summary>
/// <remarks>
/// O download do arquivo é feito por um operador: o portal do SICAR protege o
/// download com CAPTCHA e não expõe WFS nem API de download em massa. A camada
/// nacional consolidada publicada pelo MapBiomas ("CAR - Camada Completa") é a
/// entrada usual; o portal do SICAR serve para refresh por município.
/// </remarks>
public class SicarShapefileImporter
{
    private const int TamanhoLote = 2_000;

    /// <summary>Nomes de campo usados pelas diferentes origens para o mesmo dado.</summary>
    private static readonly string[] CamposCodigo = { "COD_IMOVEL", "cod_imovel", "CAR", "car", "COD_CAR" };
    private static readonly string[] CamposArea = { "NUM_AREA", "num_area", "AREA_HA", "area_ha", "AREA" };
    private static readonly string[] CamposMunicipio = { "MUNICIPIO", "municipio", "NOM_MUNICI", "NM_MUNICIP" };
    private static readonly string[] CamposUf = { "COD_ESTADO", "cod_estado", "UF", "uf", "SIGLA_UF" };
    private static readonly string[] CamposSituacao = { "IND_STATUS", "ind_status", "SITUACAO", "situacao", "STATUS" };
    private static readonly string[] CamposTipo = { "IND_TIPO", "ind_tipo", "TIPO_IMOVE", "tipo" };

    private readonly GeoDbContext _context;
    private readonly ILogger<SicarShapefileImporter> _logger;

    public SicarShapefileImporter(GeoDbContext context, ILogger<SicarShapefileImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoImportacao> ImportarAsync(
        string caminhoShapefile,
        string origem,
        string? uf = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoShapefile))
        {
            throw new FileNotFoundException("Shapefile não encontrado.", caminhoShapefile);
        }

        var carga = new CargaBaseCar
        {
            Origem = origem,
            Arquivo = Path.GetFileName(caminhoShapefile),
            HashArquivo = await CalcularHashAsync(caminhoShapefile, cancellationToken),
            Uf = uf
        };

        _context.Cargas.Add(carga);
        await _context.SaveChangesAsync(cancellationToken);

        var avisos = new List<string>();
        var lote = new List<ImovelCar>(TamanhoLote);

        try
        {
            foreach (var feature in Shapefile.ReadAllFeatures(caminhoShapefile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                carga.RegistrosLidos++;

                var imovel = Mapear(feature.Geometry, feature.Attributes, carga.Id, avisos);

                if (imovel is null)
                {
                    carga.RegistrosDescartados++;
                    continue;
                }

                lote.Add(imovel);

                if (lote.Count >= TamanhoLote)
                {
                    carga.RegistrosGravados += await GravarLoteAsync(lote, cancellationToken);
                    lote.Clear();
                }
            }

            if (lote.Count > 0)
            {
                carga.RegistrosGravados += await GravarLoteAsync(lote, cancellationToken);
            }

            carga.Status = StatusCarga.Concluida;
            carga.ConcluidaEm = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            carga.Status = StatusCarga.Falhou;
            carga.ConcluidaEm = DateTime.UtcNow;
            carga.Erro = ex.Message;
            _logger.LogError(ex, "Falha ao importar {Arquivo}", caminhoShapefile);

            _context.Cargas.Update(carga);
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        _context.Cargas.Update(carga);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Carga {CargaId} concluída: {Gravados} gravados, {Descartados} descartados de {Lidos} lidos.",
            carga.Id, carga.RegistrosGravados, carga.RegistrosDescartados, carga.RegistrosLidos);

        return new ResultadoImportacao(
            carga.Id, carga.RegistrosLidos, carga.RegistrosGravados, carga.RegistrosDescartados, avisos);
    }

    private static ImovelCar? Mapear(
        Geometry? geometria,
        NetTopologySuite.Features.IAttributesTable atributos,
        long cargaId,
        List<string> avisos)
    {
        var codigo = CodigoCar.Normalizar(Texto(atributos, CamposCodigo));

        if (codigo is null)
        {
            return null;
        }

        if (geometria is null || geometria.IsEmpty)
        {
            avisos.Add($"{codigo}: geometria ausente.");
            return null;
        }

        if (!geometria.IsValid)
        {
            // Auto-interseção é comum nos perímetros declarados; o buffer(0) resolve
            // a maioria sem alterar a área de forma relevante.
            geometria = geometria.Buffer(0);

            if (geometria.IsEmpty || !geometria.IsValid)
            {
                avisos.Add($"{codigo}: geometria inválida e não recuperável.");
                return null;
            }

            avisos.Add($"{codigo}: geometria corrigida por buffer(0).");
        }

        geometria.SRID = GeoDbContext.Srid;
        var centroide = geometria.Centroid;
        centroide.SRID = GeoDbContext.Srid;

        return new ImovelCar
        {
            CodigoCar = codigo,
            Perimetro = geometria,
            Centroide = centroide,
            AreaHa = Numero(atributos, CamposArea),
            Municipio = Texto(atributos, CamposMunicipio),
            Uf = CodigoCar.ExtrairUf(codigo) ?? Texto(atributos, CamposUf),
            CodigoIbge = CodigoCar.ExtrairCodigoIbge(codigo),
            Situacao = Texto(atributos, CamposSituacao),
            Tipo = Texto(atributos, CamposTipo),
            CargaId = cargaId
        };
    }

    /// <summary>
    /// Grava o lote com upsert por código do CAR: recarregar a base é operação
    /// rotineira e não pode falhar por chave duplicada.
    /// </summary>
    private async Task<int> GravarLoteAsync(List<ImovelCar> lote, CancellationToken cancellationToken)
    {
        var codigos = lote.Select(x => x.CodigoCar).ToList();

        var existentes = await _context.ImoveisCar
            .Where(x => codigos.Contains(x.CodigoCar))
            .ToDictionaryAsync(x => x.CodigoCar, cancellationToken);

        foreach (var imovel in lote)
        {
            if (existentes.TryGetValue(imovel.CodigoCar, out var atual))
            {
                atual.Perimetro = imovel.Perimetro;
                atual.Centroide = imovel.Centroide;
                atual.AreaHa = imovel.AreaHa;
                atual.Municipio = imovel.Municipio;
                atual.Uf = imovel.Uf;
                atual.CodigoIbge = imovel.CodigoIbge;
                atual.Situacao = imovel.Situacao;
                atual.Tipo = imovel.Tipo;
                atual.CargaId = imovel.CargaId;
                continue;
            }

            _context.ImoveisCar.Add(imovel);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();

        return lote.Count;
    }

    private static string? Texto(NetTopologySuite.Features.IAttributesTable atributos, string[] candidatos)
    {
        foreach (var nome in candidatos)
        {
            if (!atributos.Exists(nome))
            {
                continue;
            }

            var valor = atributos[nome]?.ToString();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor.Trim();
            }
        }

        return null;
    }

    private static double? Numero(NetTopologySuite.Features.IAttributesTable atributos, string[] candidatos)
    {
        var texto = Texto(atributos, candidatos);

        if (texto is null)
        {
            return null;
        }

        return double.TryParse(texto, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    private static async Task<string> CalcularHashAsync(string caminho, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(caminho);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
