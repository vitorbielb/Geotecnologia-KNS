using System.Globalization;
using System.Text;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using Microsoft.Extensions.Options;

namespace GeotecnologiaKNS.Laudos;

public record ImagemDoMapa(byte[]? Png, string? Indisponivel);

public interface IMapaDoLaudo
{
    Task<ImagemDoMapa> GerarAsync(
        string perimetroGeoJson,
        IReadOnlyList<Sobreposicao> recortes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A imagem do mapa que vai no laudo: o perímetro do imóvel e, por cima, as
/// áreas que dispararam cada regra.
/// </summary>
/// <remarks>
/// É a evidência visual do que o texto afirma. Um laudo que diz "sobreposição
/// de 7,32 ha com área embargada" e não mostra onde obriga quem o lê a confiar;
/// mostrando, ele pode conferir.
///
/// As geometrias vão codificadas em polilinha porque o endereço da imagem tem
/// limite de tamanho, e um perímetro do CAR em coordenadas soltas estoura
/// sozinho. A codificação reduz cada ponto a poucos caracteres.
/// </remarks>
public class MapaDoLaudo : IMapaDoLaudo
{
    /// <summary>Limite prático do endereço da imagem estática do Google.</summary>
    private const int LimiteDoEndereco = 8000;

    /// <summary>
    /// Cores do contorno por tipo de camada, nas mesmas famílias do sistema.
    /// </summary>
    /// <remarks>
    /// O perímetro é azul, como em toda tela. As sobreposições saem na cor da
    /// gravidade que costumam ter: embargo e unidade de conservação em
    /// vermelho, desmatamento em laranja, as demais em âmbar. Quem já viu o
    /// mapa na tela reconhece o documento.
    /// </remarks>
    private static readonly IReadOnlyDictionary<TipoCamada, string> CorPorTipo =
        new Dictionary<TipoCamada, string>
        {
            [TipoCamada.EmbargoAmbiental] = "0xC7442F",
            [TipoCamada.TerraIndigena] = "0xC7442F",
            [TipoCamada.UnidadeConservacao] = "0xC7442F",
            [TipoCamada.TerritorioQuilombola] = "0xC7442F",
            [TipoCamada.DesmatamentoConsolidado] = "0xE06C2A",
            [TipoCamada.AlertaDesmatamento] = "0xE06C2A"
        };

    private const string CorPadrao = "0xC98A00";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly GoogleMapsOptions _maps;
    private readonly ILogger<MapaDoLaudo> _logger;

    public MapaDoLaudo(IOptions<GoogleMapsOptions> maps, ILogger<MapaDoLaudo> logger)
    {
        _maps = maps.Value;
        _logger = logger;
    }

    public async Task<ImagemDoMapa> GerarAsync(
        string perimetroGeoJson,
        IReadOnlyList<Sobreposicao> recortes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_maps.ApiKey))
        {
            return new ImagemDoMapa(null, "A chave da API de mapas não está configurada.");
        }

        if (string.IsNullOrWhiteSpace(perimetroGeoJson))
        {
            return new ImagemDoMapa(null, "O imóvel não tem perímetro registrado.");
        }

        try
        {
            var endereco = MontarEndereco(perimetroGeoJson, recortes);

            if (endereco is null)
            {
                return new ImagemDoMapa(null, "O perímetro do imóvel não pôde ser desenhado.");
            }

            using var resposta = await Http.GetAsync(endereco, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                return new ImagemDoMapa(
                    null, $"O serviço de mapas respondeu {(int)resposta.StatusCode}.");
            }

            return new ImagemDoMapa(
                await resposta.Content.ReadAsByteArrayAsync(cancellationToken), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O laudo sai sem o mapa, e dizendo que saiu sem o mapa. Falhar a
            // emissão inteira por causa da imagem seria trocar um documento
            // incompleto por documento nenhum.
            _logger.LogWarning(ex, "O mapa do laudo não pôde ser gerado.");

            return new ImagemDoMapa(null, "O mapa não pôde ser gerado no momento da emissão.");
        }
    }

    private string? MontarEndereco(string perimetroGeoJson, IReadOnlyList<Sobreposicao> recortes)
    {
        var perimetro = Polilinhas.De(perimetroGeoJson);

        if (perimetro.Count == 0)
        {
            return null;
        }

        var endereco = new StringBuilder("https://maps.googleapis.com/maps/api/staticmap?");
        // Proporção escolhida para a página: o A4 com as margens do laudo dá
        // cerca de 480 pontos de largura, e 640x260 preenche isso numa faixa
        // de altura razoável. Uma imagem mais quadrada sobraria branco dos dois
        // lados ou empurraria as ocorrências para a página seguinte.
        endereco.Append("size=640x260&scale=2&maptype=hybrid&format=png");

        var caminhos = new List<string>();

        // O perímetro primeiro, para ficar por baixo das sobreposições.
        foreach (var anel in perimetro)
        {
            caminhos.Add($"&path=color:0x0E91EFFF|weight:3|fillcolor:0x0E91EF22|enc:{anel}");
        }

        // As maiores primeiro: se o endereço estourar o limite, o que se perde
        // são as sobreposições menos relevantes, não as que decidem o laudo.
        foreach (var recorte in recortes.OrderByDescending(x => x.AreaSobrepostaHa))
        {
            if (string.IsNullOrWhiteSpace(recorte.RecorteGeoJson))
            {
                continue;
            }

            var cor = CorPorTipo.TryGetValue(recorte.Tipo, out var c) ? c : CorPadrao;

            foreach (var anel in Polilinhas.De(recorte.RecorteGeoJson))
            {
                var caminho = $"&path=color:{cor}FF|weight:2|fillcolor:{cor}66|enc:{anel}";

                if (endereco.Length + caminhos.Sum(x => x.Length) + caminho.Length > LimiteDoEndereco)
                {
                    break;
                }

                caminhos.Add(caminho);
            }
        }

        foreach (var caminho in caminhos)
        {
            endereco.Append(caminho);
        }

        endereco.Append("&key=").Append(Uri.EscapeDataString(_maps.ApiKey));

        return endereco.ToString();
    }
}

/// <summary>
/// Converte anéis de GeoJSON na polilinha codificada que o mapa estático aceita.
/// </summary>
/// <remarks>
/// O endereço da imagem tem limite de tamanho, e um perímetro do CAR escrito em
/// coordenadas soltas passa dele sozinho: cada ponto custa mais de vinte
/// caracteres. Codificado, custa cinco ou seis.
/// </remarks>
public static class Polilinhas
{
    public static IReadOnlyList<string> De(string geoJson)
    {
        try
        {
            using var documento = System.Text.Json.JsonDocument.Parse(geoJson);
            var raiz = documento.RootElement;

            if (!raiz.TryGetProperty("coordinates", out var coordenadas))
            {
                return [];
            }

            var aneis = new List<string>();
            Percorrer(coordenadas, aneis);

            return aneis;
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Desce pelos aninhamentos até achar listas de pontos.
    /// </summary>
    /// <remarks>
    /// Serve para Polygon e MultiPolygon sem tratar cada um à parte: a diferença
    /// entre eles é só um nível de lista, e descer até encontrar números resolve
    /// os dois.
    /// </remarks>
    private static void Percorrer(System.Text.Json.JsonElement elemento, List<string> aneis)
    {
        if (elemento.ValueKind != System.Text.Json.JsonValueKind.Array ||
            elemento.GetArrayLength() == 0)
        {
            return;
        }

        var primeiro = elemento[0];

        if (primeiro.ValueKind == System.Text.Json.JsonValueKind.Array &&
            primeiro.GetArrayLength() > 0 &&
            primeiro[0].ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            var pontos = elemento.EnumerateArray()
                .Select(p => (Lat: p[1].GetDouble(), Lng: p[0].GetDouble()))
                .ToList();

            if (pontos.Count >= 3)
            {
                aneis.Add(Codificar(pontos));
            }

            return;
        }

        foreach (var filho in elemento.EnumerateArray())
        {
            Percorrer(filho, aneis);
        }
    }

    /// <summary>Algoritmo de polilinha codificada do Google.</summary>
    private static string Codificar(IReadOnlyList<(double Lat, double Lng)> pontos)
    {
        var texto = new StringBuilder();
        long latAnterior = 0;
        long lngAnterior = 0;

        foreach (var (lat, lng) in pontos)
        {
            var latE5 = (long)Math.Round(lat * 1e5, MidpointRounding.AwayFromZero);
            var lngE5 = (long)Math.Round(lng * 1e5, MidpointRounding.AwayFromZero);

            CodificarValor(texto, latE5 - latAnterior);
            CodificarValor(texto, lngE5 - lngAnterior);

            latAnterior = latE5;
            lngAnterior = lngE5;
        }

        return Uri.EscapeDataString(texto.ToString());
    }

    private static void CodificarValor(StringBuilder texto, long valor)
    {
        var v = valor < 0 ? ~(valor << 1) : valor << 1;

        while (v >= 0x20)
        {
            texto.Append((char)((int)((0x20 | (v & 0x1f)) + 63)));
            v >>= 5;
        }

        texto.Append((char)((int)(v + 63)));
    }
}
