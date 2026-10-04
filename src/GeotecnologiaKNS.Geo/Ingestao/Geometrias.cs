using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Preparo das geometrias que chegam das fontes externas antes de gravar.
/// </summary>
/// <remarks>
/// Compartilhado entre os importadores porque cada órgão publica de um jeito e
/// os mesmos três tratamentos valem para todos: descartar o que está vazio,
/// achatar a terceira dimensão e consertar polígono inválido.
/// </remarks>
public static class Geometrias
{
    /// <summary>
    /// Retângulo que contém o Brasil inteiro, com folga.
    /// </summary>
    /// <remarks>
    /// Do Monte Caburaí (+5,3) ao Arroio Chuí (−33,8); da Ponta do Seixas
    /// (−34,8) à nascente do Moa (−73,99). A folga de um grau existe para não
    /// recusar imóvel legítimo na fronteira.
    /// </remarks>
    private static readonly Envelope Brasil = new(-75, -33, -35, 7);

    /// <summary>
    /// Verifica se uma extensão cai dentro do Brasil.
    /// </summary>
    /// <remarks>
    /// Existe por causa de um defeito que passou silencioso: a base do CAR
    /// baixada em SHAPE-ZIP veio com latitude e longitude trocadas, e os imóveis
    /// foram parar no meio do Atlântico. Nada falhou — o cruzamento
    /// simplesmente não encontrava nada, e toda análise saía "nenhuma
    /// sobreposição". Um laudo limpo para todo fornecedor é o pior resultado
    /// possível, e é o que mais se parece com sucesso.
    /// </remarks>
    public static bool DentroDoBrasil(Envelope? extensao) =>
        extensao is { IsNull: false } && Brasil.Contains(extensao);

    /// <summary>Verifica se uma geometria cai dentro do Brasil.</summary>
    public static bool DentroDoBrasil(Geometry? geometria) =>
        geometria is { IsEmpty: false } && DentroDoBrasil(geometria.EnvelopeInternal);

    /// <summary>
    /// Devolve a geometria pronta para gravar, ou null quando não há o que
    /// aproveitar.
    /// </summary>
    public static Geometry? Normalizar(Geometry? geometria)
    {
        if (geometria is null || geometria.IsEmpty)
        {
            return null;
        }

        geometria = Achatar(geometria);

        if (!geometria.IsValid)
        {
            // Buffer de zero reconstrói o polígono resolvendo auto-interseção,
            // que é o defeito comum nestas bases. Quando nem isso salva, a
            // feição é descartada — uma área errada no laudo é pior que uma
            // feição a menos, e a contagem de descartes vai para o log.
            geometria = geometria.Buffer(0);

            if (geometria.IsEmpty || !geometria.IsValid)
            {
                return null;
            }
        }

        geometria.SRID = GeoDbContext.Srid;
        return geometria;
    }

    /// <summary>
    /// Remove a coordenada Z.
    /// </summary>
    /// <remarks>
    /// O IBGE publica os territórios quilombolas em 3D, e a coluna do PostGIS é
    /// 2D: sem isto a gravação falha inteira com "Geometry has Z dimension but
    /// column does not". A altitude não tem uso nenhum aqui — o cruzamento é de
    /// área sobre área.
    /// </remarks>
    private static Geometry Achatar(Geometry geometria)
    {
        if (!TemZ(geometria))
        {
            return geometria;
        }

        var editor = new GeometryEditor(geometria.Factory);
        return editor.Edit(geometria, new RemoverZ());
    }

    private static bool TemZ(Geometry geometria)
    {
        foreach (var coordenada in geometria.Coordinates)
        {
            if (!double.IsNaN(coordenada.Z))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class RemoverZ : GeometryEditor.CoordinateOperation
    {
        public override Coordinate[] Edit(Coordinate[] coordenadas, Geometry geometria) =>
            coordenadas.Select(c => new Coordinate(c.X, c.Y)).ToArray();
    }
}
