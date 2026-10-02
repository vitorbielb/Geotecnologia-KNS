using System.Text;
using System.Text.Json;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoImportacaoEmbargo(
    int Documentos,
    int CamadaId,
    int Lidos,
    int Cancelados,
    int SemGeometria,
    int Invalidos,
    int Gravados);

/// <summary>
/// Carrega os termos de embargo do IBAMA a partir do CSV de dados abertos.
/// </summary>
/// <remarks>
/// Importador próprio, e não o de shapefile, porque o IBAMA não publica
/// shapefile: publica CSV com a geometria em WKT numa coluna. O arquivo de
/// coordenadas que o portal oferece à parte traz os vértices soltos, um por
/// linha, e um terço dos polígonos lá tem menos de três pontos — reconstruir
/// dali produziria áreas erradas. A coluna GEOM_AREA_EMBARGADA já vem com o
/// polígono fechado.
/// </remarks>
public class EmbargoIbamaImporter
{
    private const int TamanhoLote = 2_000;

    public const string Chave = "embargo-ibama";

    private const string OrigemDaBase = "IBAMA — Termos de embargo";

    /// <summary>O IBAMA publica diariamente; recarregar semanalmente basta.</summary>
    private const int PeriodicidadeDias = 7;

    private readonly GeoDbContext _context;
    private readonly ILogger<EmbargoIbamaImporter> _logger;

    public EmbargoIbamaImporter(GeoDbContext context, ILogger<EmbargoIbamaImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoImportacaoEmbargo> ImportarAsync(
        string caminhoCsv,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoCsv))
        {
            throw new FileNotFoundException("Arquivo de termos de embargo não encontrado.", caminhoCsv);
        }

        var camada = await PrepararCamadaAsync(cancellationToken);

        // Latin1: o IBAMA publica sem BOM e com acentuação em ISO-8859-1. Ler
        // como UTF-8 troca os acentos por caracteres de substituição, e o nome
        // do embargado é justamente o que vai para o laudo.
        using var leitor = new LeitorCsv(new StreamReader(caminhoCsv, Encoding.Latin1));
        leitor.LerCabecalho();

        var iGeom = leitor.IndiceDe("GEOM_AREA_EMBARGADA");
        var iCancelado = leitor.IndiceDe("SIT_CANCELADO");
        var iTad = leitor.IndiceDe("NUM_TAD");
        var iNome = leitor.IndiceDe("NOME_EMBARGADO");
        var iCpfCnpj = leitor.IndiceDe("CPF_CNPJ_EMBARGADO");
        var iMunicipio = leitor.IndiceDe("MUNICIPIO");
        var iUf = leitor.IndiceDe("UF");
        var iData = leitor.IndiceDe("DAT_EMBARGO");
        var iArea = leitor.IndiceDe("QTD_AREA_EMBARGADA");
        var iImovel = leitor.IndiceDe("NOME_IMOVEL");

        // SRID é aplicado depois da leitura, em Interpretar.
        var troca = new TrocaDeCamada(_context, _logger);
        await troca.LimparTentativaAnteriorAsync(camada, cancellationToken);

        // O arquivo alimenta duas estruturas, e as duas trocam versionadas pelo
        // mesmo motivo: a lista por documento é o único caminho para os embargos
        // sem área delimitada, que são quase metade do total.
        var trocaDaLista = new TrocaDeListaRestritiva(_context, _logger);

        var lista = await trocaDaLista.PrepararAsync(
            TipoRestricao.EmbargoAmbiental,
            "Termos de embargo por CPF/CNPJ",
            OrigemDaBase,
            PeriodicidadeDias,
            cancellationToken);

        var versao = TrocaDeCamada.ProximaVersao(camada);
        var versaoDaLista = TrocaDeListaRestritiva.ProximaVersao(lista);

        var wkt = new WKTReader();

        var lidos = 0;
        var cancelados = 0;
        var semGeometria = 0;
        var invalidos = 0;
        var gravados = 0;
        var lote = new List<FeicaoReferencia>(TamanhoLote);
        var loteDocumentos = new List<RestricaoDocumento>(TamanhoLote);
        var documentos = 0;

        while (leitor.LerRegistro() is { } registro)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lidos++;

            if (registro.Count <= iGeom)
            {
                invalidos++;
                continue;
            }

            // Embargo cancelado não restringe nada; mantê-lo bloquearia imóvel
            // por uma autuação que o próprio IBAMA desfez.
            if (string.Equals(Campo(registro, iCancelado), "S", StringComparison.OrdinalIgnoreCase))
            {
                cancelados++;
                continue;
            }

            var textoGeometria = Campo(registro, iGeom);
            var geometria = string.IsNullOrWhiteSpace(textoGeometria)
                ? null
                : Interpretar(wkt, textoGeometria);

            // A lista por documento é alimentada mesmo sem área delimitada, e é
            // justamente aí que ela importa: quase metade dos termos do IBAMA
            // não tem polígono. Esses embargos existem, recaem sobre uma
            // pessoa, e nenhum cruzamento geográfico jamais os encontraria.
            var documento = RestricaoDocumentoService.Normalizar(Campo(registro, iCpfCnpj));

            if (documento is not null)
            {
                loteDocumentos.Add(new RestricaoDocumento
                {
                    Documento = documento,
                    NomeTitular = Truncar(Campo(registro, iNome), 250),
                    Tipo = TipoRestricao.EmbargoAmbiental,
                    Origem = OrigemDaBase,
                    Referencia = Truncar(Campo(registro, iTad), 60),
                    Municipio = Truncar(Campo(registro, iMunicipio), 150),
                    Uf = Truncar(Campo(registro, iUf), 2),
                    DataRestricao = Truncar(Campo(registro, iData), 40),
                    TemGeometria = geometria is not null,
                    Versao = versaoDaLista
                });

                documentos++;
            }

            if (loteDocumentos.Count >= TamanhoLote)
            {
                await GravarDocumentosAsync(loteDocumentos, cancellationToken);
                loteDocumentos.Clear();
            }

            if (string.IsNullOrWhiteSpace(textoGeometria))
            {
                semGeometria++;
                continue;
            }

            if (geometria is null)
            {
                invalidos++;
                continue;
            }

            lote.Add(new FeicaoReferencia
            {
                CamadaId = camada.Id,
                Versao = versao,
                Geometria = geometria,
                Rotulo = MontarRotulo(registro, iTad, iMunicipio, iUf),
                AtributosJson = SerializarAtributos(
                    registro, iTad, iNome, iCpfCnpj, iMunicipio, iUf, iData, iArea, iImovel)
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

        if (loteDocumentos.Count > 0)
        {
            await GravarDocumentosAsync(loteDocumentos, cancellationToken);
        }

        // As duas trocas são conferidas antes de qualquer uma ser publicada.
        // Publicar uma e recusar a outra deixaria a camada e a lista falando de
        // arquivos diferentes — e, como EMB-001 e EMB-002 se apoiam uma na outra
        // para não contar o mesmo embargo duas vezes, o laudo ficaria incoerente
        // consigo mesmo.
        var vereditoDaCamada = GuardaDeCarga.Avaliar(camada.TotalFeicoes, gravados);
        var vereditoDaLista = GuardaDeCarga.Avaliar(lista.TotalRegistros, documentos);

        if (vereditoDaCamada != MotivoDaRecusa.Nenhum || vereditoDaLista != MotivoDaRecusa.Nenhum)
        {
            await troca.DescartarPendenteAsync(camada, cancellationToken);
            await trocaDaLista.DescartarPendenteAsync(lista, cancellationToken);

            var recusada = vereditoDaCamada != MotivoDaRecusa.Nenhum
                ? new ResultadoDaTroca(false, vereditoDaCamada, camada.TotalFeicoes, gravados)
                : new ResultadoDaTroca(false, vereditoDaLista, lista.TotalRegistros, documentos, "registros");

            _logger.LogError("Recarga dos embargos recusada: {Explicacao}", recusada.Explicacao);

            throw new InvalidOperationException(recusada.Explicacao);
        }

        await troca.PublicarAsync(camada, gravados, cancellationToken);
        await trocaDaLista.PublicarAsync(lista, documentos, cancellationToken);

        _logger.LogInformation(
            "Embargos do IBAMA: {Gravados} gravados de {Lidos} lidos " +
            "({Cancelados} cancelados, {SemGeometria} sem geometria, {Invalidos} inválidos).",
            gravados, lidos, cancelados, semGeometria, invalidos);

        return new ResultadoImportacaoEmbargo(
            documentos, camada.Id, lidos, cancelados, semGeometria, invalidos, gravados);
    }

    private async Task<CamadaReferencia> PrepararCamadaAsync(CancellationToken cancellationToken)
    {
        var camada = await _context.Camadas.FirstOrDefaultAsync(x => x.Chave == Chave, cancellationToken);

        if (camada is null)
        {
            camada = new CamadaReferencia { Chave = Chave };
            _context.Camadas.Add(camada);
        }
        camada.Nome = "Termos de embargo";
        camada.Tipo = TipoCamada.EmbargoAmbiental;
        camada.Origem = "IBAMA — Dados Abertos (termo_de_embargo)";
        camada.Ativa = true;

        await _context.SaveChangesAsync(cancellationToken);
        return camada;
    }

    private static Geometry? Interpretar(WKTReader leitor, string texto)
    {
        Geometry geometria;

        try
        {
            geometria = leitor.Read(texto);
        }
        catch (Exception)
        {
            // WKT truncado ou malformado acontece nesta base; descartar a feição
            // é melhor que abortar a carga inteira.
            return null;
        }

        return Geometrias.Normalizar(geometria);
    }

    private static string Campo(IReadOnlyList<string> registro, int indice) =>
        indice >= 0 && indice < registro.Count ? registro[indice].Trim() : string.Empty;

    private static string MontarRotulo(
        IReadOnlyList<string> registro, int iTad, int iMunicipio, int iUf)
    {
        var tad = Campo(registro, iTad);
        var municipio = Campo(registro, iMunicipio);
        var uf = Campo(registro, iUf);

        var texto = new StringBuilder("TAD ");
        texto.Append(string.IsNullOrWhiteSpace(tad) ? "sem número" : tad);

        if (!string.IsNullOrWhiteSpace(municipio))
        {
            texto.Append(" — ").Append(municipio);

            if (!string.IsNullOrWhiteSpace(uf))
            {
                texto.Append('/').Append(uf);
            }
        }

        return texto.ToString();
    }

    /// <summary>
    /// Subconjunto útil dos atributos de origem.
    /// </summary>
    /// <remarks>
    /// O arquivo tem trinta colunas, quase todas de controle interno do IBAMA.
    /// Guardar todas engordaria o jsonb em milhões de linhas sem servir ao
    /// laudo. O CPF/CNPJ fica porque é o que liga o embargo a uma pessoa — a
    /// mesma chave de que a checagem por documento vai precisar.
    /// </remarks>
    private static string SerializarAtributos(
        IReadOnlyList<string> registro,
        int iTad, int iNome, int iCpfCnpj, int iMunicipio, int iUf, int iData, int iArea, int iImovel)
    {
        var atributos = new Dictionary<string, string?>
        {
            ["num_tad"] = Campo(registro, iTad),
            ["embargado"] = Campo(registro, iNome),
            ["cpf_cnpj"] = Campo(registro, iCpfCnpj),
            ["municipio"] = Campo(registro, iMunicipio),
            ["uf"] = Campo(registro, iUf),
            ["data_embargo"] = Campo(registro, iData),
            ["area_embargada"] = Campo(registro, iArea),
            ["imovel"] = Campo(registro, iImovel)
        };

        foreach (var vazio in atributos.Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToList())
        {
            atributos.Remove(vazio);
        }

        return JsonSerializer.Serialize(atributos);
    }

    private async Task GravarDocumentosAsync(
        List<RestricaoDocumento> lote, CancellationToken cancellationToken)
    {
        _context.RestricoesPorDocumento.AddRange(lote);
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private static string? Truncar(string? valor, int tamanho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return valor.Length <= tamanho ? valor : valor[..tamanho];
    }

    private async Task<int> GravarLoteAsync(List<FeicaoReferencia> lote, CancellationToken cancellationToken)
    {
        _context.Feicoes.AddRange(lote);
        await _context.SaveChangesAsync(cancellationToken);

        // Sem limpar o rastreador, o consumo cresce linearmente com o arquivo.
        _context.ChangeTracker.Clear();

        return lote.Count;
    }
}
