using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Resolve e grava a região que uma camada regional cobre.
/// </summary>
/// <remarks>
/// A abrangência é o que impede uma regra de se dizer avaliada onde a camada
/// dela não alcança. Sem ela, o PRODES da Amazônia respondia "estou carregado"
/// para um imóvel de Goiás — e o imóvel saía liberado por uma verificação que
/// nunca aconteceu.
///
/// O recorte sai da camada de biomas, carregada do mesmo INPE que publica o
/// PRODES. Usar o limite do próprio provedor importa: duas fontes quase iguais
/// deixariam uma faixa de desacordo bem na divisa, e divisa de bioma é onde
/// ficam Goiás, Tocantins e Mato Grosso.
/// </remarks>
public class AbrangenciaDeCamada
{
    private readonly GeoDbContext _context;

    public AbrangenciaDeCamada(GeoDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Grava na camada a união dos biomas declarados, ou nulo para nacional.
    /// </summary>
    /// <remarks>
    /// Recusa em vez de aceitar quando os biomas não podem ser resolvidos. É
    /// deliberado: gravar nulo ali significaria "cobre o Brasil inteiro", que é
    /// exatamente a mentira que este campo existe para impedir. Entre falhar a
    /// carga e publicar uma camada que exagera o próprio alcance, falhar é o
    /// que protege.
    /// </remarks>
    public async Task DefinirAsync(
        CamadaReferencia camada,
        IReadOnlyList<string>? biomas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(camada);

        if (biomas is null || biomas.Count == 0)
        {
            // Cobertura nacional: nada a recortar.
            camada.Abrangencia = null;
            return;
        }

        var limites = await _context.Database
            .SqlQuery<byte[]?>($@"
                SELECT ST_AsBinary(ST_Union(f.geometria)) AS ""Value""
                FROM geo.feicao_referencia f
                JOIN geo.camada_referencia b
                  ON b.id = f.camada_id AND f.versao = b.versao_atual
                WHERE b.chave = {CatalogoDeFontes.BiomasDoBrasil}
                  AND f.rotulo = ANY({biomas.ToArray()})")
            .ToListAsync(cancellationToken);

        var wkb = limites.Count > 0 ? limites[0] : null;

        if (wkb is null || wkb.Length == 0)
        {
            throw new InvalidOperationException(
                $"A camada {camada.Chave} declara cobrir {string.Join(", ", biomas)}, mas os " +
                $"limites desses biomas não estão carregados. Carregue antes a camada " +
                $"'{CatalogoDeFontes.BiomasDoBrasil}'. A camada não foi publicada: sem o " +
                "recorte ela se diria nacional, e uma regra se diria avaliada em imóvel que " +
                "esta camada não alcança.");
        }

        camada.Abrangencia = new NetTopologySuite.IO.WKBReader().Read(wkb);
        camada.Abrangencia.SRID = GeoDbContext.Srid;
    }
}
