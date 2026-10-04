using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record CampoShapefile(string Nome, string? Exemplo, bool Reconhecido, string? MapeadoPara);

public record InspecaoShapefile(
    string Arquivo,
    int RegistrosAmostrados,
    string? TipoGeometria,
    IReadOnlyList<CampoShapefile> Campos,
    IReadOnlyList<string> Problemas,
    Envelope? Extensao = null);

/// <summary>
/// Lê apenas o cabeçalho e as primeiras feições de um shapefile para mostrar
/// quais colunas existem no .dbf e quais delas o importador reconhece.
/// </summary>
/// <remarks>
/// Existe porque cada origem (SICAR, MapBiomas, SEMAs estaduais) nomeia as
/// mesmas colunas de forma diferente. Sem conferir antes, a importação
/// descartaria todos os registros sem explicar o motivo.
/// </remarks>
public static class ShapefileInspector
{
    public static InspecaoShapefile Inspecionar(string caminhoShapefile, int amostra = 3)
    {
        if (!File.Exists(caminhoShapefile))
        {
            throw new FileNotFoundException("Shapefile não encontrado.", caminhoShapefile);
        }

        var campos = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var problemas = new List<string>();
        string? tipoGeometria = null;
        var lidos = 0;

        // A extensão da amostra é o que denuncia coordenada trocada antes de a
        // carga acontecer. Custou caro aprender: a base do CAR baixada em
        // SHAPE-ZIP veio com latitude e longitude invertidas, e como ninguém
        // olhava o retângulo, 1,25 milhão de imóveis foram parar no meio do
        // Atlântico. Toda análise devolvia "nenhuma sobreposição" — laudo limpo
        // para todo fornecedor, sem erro nenhum aparecer.
        var extensao = new Envelope();

        // Crítico ser em fluxo: ReadAllFeatures leria o arquivo todo antes de o
        // laço poder parar na amostra. Inspecionar a base nacional do CAR, que é
        // justamente o primeiro passo, estouraria a memória.
        using var leitor = Shapefile.OpenRead(caminhoShapefile);

        foreach (var feature in leitor)
        {
            tipoGeometria ??= feature.Geometry?.GeometryType;

            if (feature.Geometry is { IsEmpty: false } geometria)
            {
                extensao.ExpandToInclude(geometria.EnvelopeInternal);
            }

            foreach (var nome in feature.Attributes.GetNames())
            {
                var valor = feature.Attributes[nome]?.ToString();

                if (!campos.TryGetValue(nome, out var exemplo) || string.IsNullOrWhiteSpace(exemplo))
                {
                    campos[nome] = valor;
                }
            }

            if (++lidos >= amostra)
            {
                break;
            }
        }

        if (lidos == 0)
        {
            problemas.Add("O shapefile não tem nenhuma feição.");
        }
        else if (!extensao.IsNull && !Geometrias.DentroDoBrasil(extensao))
        {
            problemas.Add(
                $"A amostra está fora do Brasil: longitude de {extensao.MinX:F4} a {extensao.MaxX:F4}, " +
                $"latitude de {extensao.MinY:F4} a {extensao.MaxY:F4}. " +
                "Suspeite de latitude e longitude trocadas, ou de sistema de coordenadas diferente " +
                "do esperado.");
        }

        var mapeamento = SicarShapefileImporter.MapeamentoDeCampos;

        var resultado = campos
            .Select(campo =>
            {
                var destino = mapeamento
                    .FirstOrDefault(m => m.Value.Contains(campo.Key, StringComparer.OrdinalIgnoreCase))
                    .Key;

                return new CampoShapefile(campo.Key, campo.Value, destino is not null, destino);
            })
            .OrderByDescending(c => c.Reconhecido)
            .ThenBy(c => c.Nome)
            .ToList();

        foreach (var (destino, _) in mapeamento)
        {
            if (!resultado.Any(c => c.MapeadoPara == destino))
            {
                problemas.Add($"Nenhuma coluna reconhecida para '{destino}'.");
            }
        }

        return new InspecaoShapefile(
            Path.GetFileName(caminhoShapefile), lidos, tipoGeometria, resultado, problemas,
            extensao.IsNull ? null : extensao);
    }
}
