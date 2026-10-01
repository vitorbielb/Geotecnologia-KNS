using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Geo.Services;

/// <summary>Uma restrição encontrada em nome da pessoa consultada.</summary>
public record AchadoPorDocumento(
    string Documento,
    TipoRestricao Tipo,
    string Origem,
    string? NomeTitular,
    string? Referencia,
    string? Municipio,
    string? Uf,
    string? DataRestricao,
    bool TemGeometria);

public interface IRestricaoDocumentoService
{
    /// <summary>
    /// Restrições registradas em nome do documento informado.
    /// </summary>
    Task<IReadOnlyList<AchadoPorDocumento>> ConsultarAsync(
        string? documento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tipos de lista restritiva que estão carregados.
    /// </summary>
    /// <remarks>
    /// Mesma razão da lista de camadas verificadas: sem saber o que existe, não
    /// se distingue "nada encontrado" de "não havia onde procurar".
    /// </remarks>
    Task<IReadOnlyList<TipoRestricao>> ObterTiposDisponiveisAsync(
        CancellationToken cancellationToken = default);
}

public class RestricaoDocumentoService : IRestricaoDocumentoService
{
    private readonly GeoDbContext _context;

    public RestricaoDocumentoService(GeoDbContext context) => _context = context;

    /// <summary>Deixa só os dígitos — é como a lista é gravada.</summary>
    public static string? Normalizar(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return null;
        }

        var digitos = new string(documento.Where(char.IsDigit).ToArray());

        // CPF tem 11, CNPJ tem 14. Qualquer outra coisa é erro de digitação ou
        // campo preenchido com outra informação, e consultar com isso só traria
        // ruído.
        if (digitos.Length is not (11 or 14))
        {
            return null;
        }

        // A base do IBAMA tem termos lavrados com 00000000000 quando o autuado
        // não foi identificado. Aceitar esse valor faria qualquer produtor
        // cadastrado com documento de preenchimento casar com dezenas de
        // embargos que não são dele — e bloquear quem está limpo é pior que
        // deixar passar quem não está.
        return digitos.Distinct().Count() == 1 ? null : digitos;
    }

    public async Task<IReadOnlyList<AchadoPorDocumento>> ConsultarAsync(
        string? documento, CancellationToken cancellationToken = default)
    {
        var normalizado = Normalizar(documento);

        if (normalizado is null)
        {
            return Array.Empty<AchadoPorDocumento>();
        }

        return await _context.RestricoesPorDocumento
            .AsNoTracking()
            .Where(x => x.Documento == normalizado)
            .OrderBy(x => x.Tipo)
            .ThenBy(x => x.Referencia)
            .Select(x => new AchadoPorDocumento(
                x.Documento, x.Tipo, x.Origem, x.NomeTitular, x.Referencia,
                x.Municipio, x.Uf, x.DataRestricao, x.TemGeometria))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TipoRestricao>> ObterTiposDisponiveisAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.RestricoesPorDocumento
            .AsNoTracking()
            .Select(x => x.Tipo)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
