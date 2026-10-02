using System.Text;
using System.Text.Json;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Carrega uma camada de referência a partir de um FeatureCollection GeoJSON.
/// </summary>
/// <remarks>
/// Existe porque nem todo órgão publica shapefile. O CNUC, das unidades de
/// conservação, só exporta GeoJSON — e num arquivo de 227 MB, que precisa ser
/// lido feição a feição.
/// </remarks>
public class CamadaGeoJsonImporter
{
    private const int TamanhoLote = 1_000;

    /// <summary>Campos usados como rótulo, na ordem de preferência.</summary>
    private static readonly string[] CamposRotulo =
    {
        "nome", "NOME", "uc_nom", "no_uc", "nome_uc", "nom_uc", "nome_proje",
        "terrai_nom", "municipio", "MUNICIPIO"
    };

    /// <summary>Campos que qualificam o achado, entre parênteses no rótulo.</summary>
    private static readonly string[] CamposQualificador =
    {
        "catmanej_nome", "categoria", "CATEGORIA", "grupo", "GRUPO",
        "catiucn_nome", "esfera", "situacao"
    };

    private readonly GeoDbContext _context;
    private readonly ILogger<CamadaGeoJsonImporter> _logger;

    public CamadaGeoJsonImporter(GeoDbContext context, ILogger<CamadaGeoJsonImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoImportacaoCamada> ImportarAsync(
        string caminhoGeoJson,
        string chave,
        string nome,
        TipoCamada tipo,
        string origem,
        int? anoReferencia = null,
        int? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoGeoJson))
        {
            throw new FileNotFoundException("Arquivo GeoJSON não encontrado.", caminhoGeoJson);
        }

        var camada = await PrepararCamadaAsync(
            chave, nome, tipo, origem, anoReferencia, tenantId, cancellationToken);

        var troca = new TrocaDeCamada(_context, _logger);
        await troca.LimparTentativaAnteriorAsync(camada, cancellationToken);

        var versao = TrocaDeCamada.ProximaVersao(camada);

        var leitorGeoJson = new GeoJsonReader();
        var lidos = 0;
        var descartados = 0;
        var gravados = 0;
        var lote = new List<FeicaoReferencia>(TamanhoLote);

        using var leitor = new LeitorGeoJson(new StreamReader(caminhoGeoJson, Encoding.UTF8));

        foreach (var textoDaFeicao in leitor.LerFeicoes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            lidos++;

            Feature? feicao;

            try
            {
                feicao = leitorGeoJson.Read<Feature>(textoDaFeicao);
            }
            catch (Exception)
            {
                // Feição malformada não pode abortar a carga inteira.
                descartados++;
                continue;
            }

            var geometria = Geometrias.Normalizar(feicao?.Geometry);

            if (geometria is null)
            {
                descartados++;
                continue;
            }

            lote.Add(new FeicaoReferencia
            {
                CamadaId = camada.Id,
                Versao = versao,
                Geometria = geometria,
                Rotulo = ExtrairRotulo(feicao!.Attributes),
                AtributosJson = SerializarAtributos(feicao.Attributes)
            });

            if (lote.Count >= TamanhoLote)
            {
                gravados += await GravarLoteAsync(lote, cancellationToken);
                lote.Clear();
            }
        }

        if (lote.Count > 0)
        {
            gravados += await GravarLoteAsync(lote, cancellationToken);
        }

        var publicacao = await troca.PublicarAsync(camada, gravados, cancellationToken);

        if (!publicacao.Aceita)
        {
            throw new InvalidOperationException(publicacao.Explicacao);
        }

        _logger.LogInformation(
            "Camada {Chave}: {Gravados} feições gravadas, {Descartados} descartadas de {Lidos} lidas.",
            chave, gravados, descartados, lidos);

        return new ResultadoImportacaoCamada(camada.Id, chave, lidos, gravados, descartados);
    }

    private async Task<CamadaReferencia> PrepararCamadaAsync(
        string chave, string nome, TipoCamada tipo, string origem, int? anoReferencia,
        int? tenantId, CancellationToken cancellationToken)
    {
        var camada = await _context.Camadas.FirstOrDefaultAsync(x => x.Chave == chave, cancellationToken);

        if (camada is null)
        {
            camada = new CamadaReferencia { Chave = chave };
            _context.Camadas.Add(camada);
        }

        camada.Nome = nome;
        camada.Tipo = tipo;
        camada.Origem = origem;
        camada.TenantId = tenantId;
        camada.AnoReferencia = anoReferencia;
        camada.Ativa = true;
        camada.AtualizadaEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return camada;
    }

    private static string? ExtrairRotulo(IAttributesTable? atributos)
    {
        if (atributos is null)
        {
            return null;
        }

        var nome = PrimeiroPreenchido(atributos, CamposRotulo);

        if (nome is null)
        {
            return null;
        }

        var qualificador = PrimeiroPreenchido(atributos, CamposQualificador);
        var rotulo = qualificador is null ? nome : $"{nome} ({qualificador})";

        return rotulo.Length > 300 ? rotulo[..300] : rotulo;
    }

    private static string? PrimeiroPreenchido(IAttributesTable atributos, string[] candidatos)
    {
        var nomes = atributos.GetNames();

        foreach (var candidato in candidatos)
        {
            var nomeReal = nomes.FirstOrDefault(
                n => string.Equals(n, candidato, StringComparison.OrdinalIgnoreCase));

            if (nomeReal is null)
            {
                continue;
            }

            var valor = atributos[nomeReal]?.ToString()?.Trim();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return null;
    }

    private static string SerializarAtributos(IAttributesTable? atributos)
    {
        if (atributos is null)
        {
            return "{}";
        }

        var dicionario = new Dictionary<string, string?>();

        foreach (var nome in atributos.GetNames())
        {
            var valor = atributos[nome]?.ToString();

            // Texto longo é descrição de objetivo e plano de manejo: não serve
            // ao laudo e multiplicaria o tamanho do jsonb por dezenas de
            // milhares de linhas.
            if (valor is { Length: > 300 })
            {
                continue;
            }

            dicionario[nome] = valor;
        }

        return JsonSerializer.Serialize(dicionario);
    }

    private async Task<int> GravarLoteAsync(List<FeicaoReferencia> lote, CancellationToken cancellationToken)
    {
        _context.Feicoes.AddRange(lote);
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();

        return lote.Count;
    }
}
