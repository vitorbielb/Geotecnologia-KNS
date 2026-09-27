using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Geo.Services;

/// <summary>
/// Dados de um imóvel resolvidos a partir do número do CAR.
/// É o que o cadastro de propriedade passa a preencher sozinho.
/// </summary>
public record ImovelCarDto(
    string CodigoCar,
    string PerimetroGeoJson,
    double CentroLat,
    double CentroLng,
    double? AreaHa,
    double? AreaCalculadaHa,
    string? Municipio,
    string? Uf,
    string? Situacao,
    string? Tipo,
    DateTime? AtualizadoEmOrigem,
    string Origem,
    DateTime? BaseCarregadaEm);

public interface ICarLookupService
{
    /// <summary>
    /// Busca o imóvel na base do CAR. Retorna null quando o código não existe na
    /// versão carregada — o que não significa CAR inválido, e sim base desatualizada.
    /// </summary>
    Task<ImovelCarDto?> ObterPorCodigoAsync(string codigoCar, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se o acesso ao PostGIS está configurado neste servidor.
    /// Distinto de <see cref="BaseDisponivelAsync"/>: sem configuração não há
    /// nem para onde perguntar, e a causa que o administrador precisa tratar
    /// é outra.
    /// </summary>
    bool EstaConfigurado { get; }

    /// <summary>Indica se há alguma carga concluída, isto é, se a base está utilizável.</summary>
    Task<bool> BaseDisponivelAsync(CancellationToken cancellationToken = default);
}

public class CarLookupService : ICarLookupService
{
    private readonly GeoDbContext _context;

    public CarLookupService(GeoDbContext context)
    {
        _context = context;
    }

    public async Task<ImovelCarDto?> ObterPorCodigoAsync(string codigoCar, CancellationToken cancellationToken = default)
    {
        var codigoNormalizado = CodigoCar.Normalizar(codigoCar);

        if (codigoNormalizado is null)
        {
            return null;
        }

        var imovel = await _context.ImoveisCar
            .AsNoTracking()
            .Include(x => x.Carga)
            .FirstOrDefaultAsync(x => x.CodigoCar == codigoNormalizado, cancellationToken);

        if (imovel is null)
        {
            return null;
        }

        var centroide = imovel.Centroide ?? imovel.Perimetro.Centroid;
        var geoJsonWriter = new NetTopologySuite.IO.GeoJsonWriter();

        return new ImovelCarDto(
            imovel.CodigoCar,
            geoJsonWriter.Write(imovel.Perimetro),
            centroide.Y,
            centroide.X,
            imovel.AreaHa,
            imovel.AreaCalculadaHa,
            imovel.Municipio,
            imovel.Uf,
            imovel.Situacao,
            imovel.Tipo,
            imovel.AtualizadoEmOrigem,
            imovel.Carga?.Origem ?? string.Empty,
            imovel.Carga?.ConcluidaEm);
    }

    public bool EstaConfigurado => true;

    public Task<bool> BaseDisponivelAsync(CancellationToken cancellationToken = default)
    {
        return _context.Cargas.AnyAsync(x => x.Status == StatusCarga.Concluida, cancellationToken);
    }
}
