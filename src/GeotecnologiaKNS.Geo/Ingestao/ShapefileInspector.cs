using NetTopologySuite.IO.Esri;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record CampoShapefile(string Nome, string? Exemplo, bool Reconhecido, string? MapeadoPara);

public record InspecaoShapefile(
    string Arquivo,
    int RegistrosAmostrados,
    string? TipoGeometria,
    IReadOnlyList<CampoShapefile> Campos,
    IReadOnlyList<string> Problemas);

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

        // Crítico ser em fluxo: ReadAllFeatures leria o arquivo todo antes de o
        // laço poder parar na amostra. Inspecionar a base nacional do CAR, que é
        // justamente o primeiro passo, estouraria a memória.
        using var leitor = Shapefile.OpenRead(caminhoShapefile);

        foreach (var feature in leitor)
        {
            tipoGeometria ??= feature.Geometry?.GeometryType;

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
            Path.GetFileName(caminhoShapefile), lidos, tipoGeometria, resultado, problemas);
    }
}
