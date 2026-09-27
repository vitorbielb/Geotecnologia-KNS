using System.Text.Json;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoImportacaoCamada(
    int CamadaId,
    string Chave,
    int Lidos,
    int Gravados,
    int Descartados);

/// <summary>
/// Carrega uma camada de referência (embargos, TI, UC, PRODES...) a partir de
/// um shapefile publicado pelo órgão de origem.
/// </summary>
/// <remarks>
/// É um caminho de ingestão único para todas as camadas de propósito: cada órgão
/// publica seu shapefile com um esquema diferente, e manter um importador por
/// órgão viraria seis importadores divergentes. Os atributos da origem são
/// preservados íntegros em jsonb, e o laudo cita o que for relevante.
/// </remarks>
public class CamadaShapefileImporter
{
    private const int TamanhoLote = 2_000;

    /// <summary>Campos usados como rótulo legível, na ordem de preferência.</summary>
    private static readonly string[] CamposRotulo =
    {
        "nome", "NOME", "terrai_nom", "no_uc", "NOME_UC", "nom_uc",
        "num_tad", "NUM_TAD", "des_infrac", "cod_imovel", "municipio", "MUNICIPIO"
    };

    private readonly GeoDbContext _context;
    private readonly ILogger<CamadaShapefileImporter> _logger;

    public CamadaShapefileImporter(GeoDbContext context, ILogger<CamadaShapefileImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoImportacaoCamada> ImportarAsync(
        string caminhoShapefile,
        string chave,
        string nome,
        TipoCamada tipo,
        string origem,
        int? anoReferencia = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoShapefile))
        {
            throw new FileNotFoundException("Shapefile não encontrado.", caminhoShapefile);
        }

        var camada = await _context.Camadas.FirstOrDefaultAsync(x => x.Chave == chave, cancellationToken);

        if (camada is null)
        {
            camada = new CamadaReferencia { Chave = chave };
            _context.Camadas.Add(camada);
        }
        else
        {
            // Recarga substitui a camada inteira: manter feições da versão
            // anterior produziria sobreposições duplicadas no laudo.
            // DELETE direto porque carregar milhões de feições para o
            // ChangeTracker só para apagá-las não terminaria.
            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM geo.feicao_referencia WHERE camada_id = {0}",
                new object[] { camada.Id },
                cancellationToken);
        }

        camada.Nome = nome;
        camada.Tipo = tipo;
        camada.Origem = origem;
        camada.AnoReferencia = anoReferencia;
        camada.Ativa = true;
        camada.AtualizadaEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var lidos = 0;
        var descartados = 0;
        var gravados = 0;
        var lote = new List<FeicaoReferencia>(TamanhoLote);

        // Leitura em fluxo: ReadAllFeatures materializaria o shapefile inteiro,
        // e camadas como o PRODES Cerrado passam de dois milhões de polígonos.
        using var leitor = Shapefile.OpenRead(caminhoShapefile);

        foreach (var feature in leitor)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lidos++;

            var geometria = Normalizar(feature.Geometry);

            if (geometria is null)
            {
                descartados++;
                continue;
            }

            lote.Add(new FeicaoReferencia
            {
                CamadaId = camada.Id,
                Geometria = geometria,
                Rotulo = ExtrairRotulo(feature.Attributes),
                AtributosJson = SerializarAtributos(feature.Attributes)
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

        camada.TotalFeicoes = gravados;
        _context.Camadas.Update(camada);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Camada {Chave}: {Gravados} feições gravadas, {Descartados} descartadas de {Lidos} lidas.",
            chave, gravados, descartados, lidos);

        return new ResultadoImportacaoCamada(camada.Id, chave, lidos, gravados, descartados);
    }

    private static Geometry? Normalizar(Geometry? geometria)
    {
        if (geometria is null || geometria.IsEmpty)
        {
            return null;
        }

        if (!geometria.IsValid)
        {
            geometria = geometria.Buffer(0);

            if (geometria.IsEmpty || !geometria.IsValid)
            {
                return null;
            }
        }

        geometria.SRID = GeoDbContext.Srid;
        return geometria;
    }

    private static string? ExtrairRotulo(NetTopologySuite.Features.IAttributesTable atributos)
    {
        var nomes = atributos.GetNames();

        foreach (var candidato in CamposRotulo)
        {
            var nomeReal = nomes.FirstOrDefault(n => string.Equals(n, candidato, StringComparison.OrdinalIgnoreCase));

            if (nomeReal is null)
            {
                continue;
            }

            var valor = atributos[nomeReal]?.ToString()?.Trim();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor.Length > 300 ? valor[..300] : valor;
            }
        }

        return null;
    }

    private static string SerializarAtributos(NetTopologySuite.Features.IAttributesTable atributos)
    {
        var dicionario = new Dictionary<string, object?>();

        foreach (var nome in atributos.GetNames())
        {
            var valor = atributos[nome];

            // DateTime e afins viram texto: o jsonb é para leitura do laudo,
            // não para aritmética.
            dicionario[nome] = valor switch
            {
                null => null,
                double or float or decimal or int or long or bool => valor,
                _ => valor.ToString()
            };
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
