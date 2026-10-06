using System.Text.Json;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;

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
        "nome", "NOME", "terrai_nom", "no_uc", "NOME_UC", "nom_uc", "nome_proje", "nm_tq",
        "num_tad", "NUM_TAD", "des_infrac", "cod_imovel", "municipio", "MUNICIPIO", "municipality", "MUNICIPALITY",

        // MapBiomas Alerta: o alerta não tem nome, e o município é o que
        // permite reconhecê-lo no laudo.
        "cities", "CITIES",

        // Por último, porque várias camadas trazem o bioma como atributo e ele
        // só serve de rótulo quando não há mais nada — na camada de limites de
        // biomas, onde é o próprio nome da feição e o que permite recortar a
        // abrangência das camadas regionais.
        "bioma", "BIOMA"
    };

    /// <summary>
    /// Campos de onde sai o ano do fato, na ordem de preferência.
    /// </summary>
    /// <remarks>
    /// O corte temporal de uma regra recai sobre o fato, não sobre a carga.
    /// Enquanto cada camada guardava um único ano dava para ler o ano da
    /// camada inteira; com o PRODES trazendo de 2008 em diante, isso passaria a
    /// responder o mesmo para todo polígono.
    ///
    /// Os nomes vêm truncados em dez caracteres porque é o limite de coluna do
    /// DBF, e todo shapefile sai assim: "detected_at" chega como "detected_a".
    /// Os dois estão na lista — o GeoJSON das mesmas origens traz o nome
    /// inteiro.
    /// </remarks>
    private static readonly string[] CamposAno =
    {
        "year", "YEAR", "ano", "ANO", "year_detec", "year_detected_at",
        "detected_a", "detected_at", "view_date", "VIEW_DATE", "data_detec", "dt_detec"
    };

    /// <summary>
    /// Campos que qualificam o achado, acrescentados ao rótulo entre parênteses.
    /// </summary>
    /// <remarks>
    /// A fase da terra indígena e a categoria da unidade de conservação mudam o
    /// que a sobreposição significa juridicamente. Sem isso o laudo diria apenas
    /// o nome, e "regularizada" ficaria indistinguível de "em estudo".
    /// </remarks>
    private static readonly string[] CamposQualificador =
    {
        "fase_ti", "FASE_TI", "categoria", "CATEGORIA", "situacao", "SITUACAO", "status", "STATUS",
        "grupo", "GRUPO", "classe", "CLASSE", "classname", "CLASSNAME"
    };

    private readonly GeoDbContext _context;
    private readonly ILogger<CamadaShapefileImporter> _logger;

    public CamadaShapefileImporter(GeoDbContext context, ILogger<CamadaShapefileImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Carrega um único shapefile.</summary>
    public Task<ResultadoImportacaoCamada> ImportarAsync(
        string caminhoShapefile,
        string chave,
        string nome,
        TipoCamada tipo,
        string origem,
        int? anoReferencia = null,
        int? tenantId = null,
        IReadOnlyList<string>? biomas = null,
        int? esperadoNaOrigem = null,
        CancellationToken cancellationToken = default) =>
        ImportarAsync(
            new[] { caminhoShapefile }, chave, nome, tipo, origem,
            anoReferencia, tenantId, biomas, esperadoNaOrigem, cancellationToken);

    /// <summary>
    /// Carrega uma camada a partir de um ou mais shapefiles, numa única versão.
    /// </summary>
    /// <remarks>
    /// Mais de um arquivo quando a origem limita o tamanho da resposta e o
    /// download sai paginado. As páginas são pedaços de uma carga só: entram
    /// todas na mesma versão e a publicação acontece uma vez, no fim. Publicar
    /// página a página deixaria a análise rodando contra uma fração da camada
    /// entre uma e outra.
    /// </remarks>
    public async Task<ResultadoImportacaoCamada> ImportarAsync(
        IReadOnlyList<string> caminhos,
        string chave,
        string nome,
        TipoCamada tipo,
        string origem,
        int? anoReferencia = null,
        int? tenantId = null,
        IReadOnlyList<string>? biomas = null,
        int? esperadoNaOrigem = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caminhos);

        foreach (var caminho in caminhos)
        {
            if (!File.Exists(caminho))
            {
                throw new FileNotFoundException("Shapefile não encontrado.", caminho);
            }
        }

        var camada = await _context.Camadas.FirstOrDefaultAsync(x => x.Chave == chave, cancellationToken);

        if (camada is null)
        {
            camada = new CamadaReferencia { Chave = chave };
            _context.Camadas.Add(camada);
        }
        camada.Nome = nome;
        camada.Tipo = tipo;
        camada.Origem = origem;
        camada.AnoReferencia = anoReferencia;
        camada.Ativa = true;
        camada.TenantId = tenantId;

        await _context.SaveChangesAsync(cancellationToken);

        var troca = new TrocaDeCamada(_context, _logger);
        await troca.LimparTentativaAnteriorAsync(camada, cancellationToken);

        var versao = TrocaDeCamada.ProximaVersao(camada);

        var lidos = 0;
        var descartados = 0;
        var ilegiveis = new Contador();
        var gravados = 0;
        var menorAno = int.MaxValue;
        var lote = new List<FeicaoReferencia>(TamanhoLote);

        foreach (var caminho in caminhos)
        {
            // Leitura em fluxo: ReadAllFeatures materializaria o shapefile
            // inteiro, e o PRODES do Cerrado passa de um milhão e meio de
            // polígonos. A codificação entra aqui porque a biblioteca assume
            // UTF-8 quando o shapefile não traz .cpg/.cst, e órgão brasileiro
            // publica em Latin1.
            using var leitor = Shapefile.OpenRead(
                caminho,
                new ShapefileReaderOptions { Encoding = CodificacaoDeShapefile.Detectar(caminho) });

            foreach (var feature in Legiveis(leitor, ilegiveis, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                lidos++;

                var geometria = Geometrias.Normalizar(feature.Geometry);

                if (geometria is null)
                {
                    descartados++;
                    continue;
                }

                var ano = ExtrairAnoDe(feature.Attributes);

                if (ano is int a && a < menorAno)
                {
                    menorAno = a;
                }

                lote.Add(new FeicaoReferencia
                {
                    CamadaId = camada.Id,
                    Versao = versao,
                    Geometria = geometria,
                    Ano = ano,
                    Rotulo = ExtrairRotulo(feature.Attributes),
                    AtributosJson = SerializarAtributos(feature.Attributes)
                });

                if (lote.Count >= TamanhoLote)
                {
                    gravados += await GravarLoteAsync(lote, cancellationToken);
                    lote.Clear();
                }
            }
        }

        if (lote.Count > 0)
        {
            gravados += await GravarLoteAsync(lote, cancellationToken);
        }

        GuardaDeCarga.ConferirContraOrigem(chave, esperadoNaOrigem, lidos);

        // Antes de publicar, a camada precisa declarar o que cobre — no espaço
        // e no tempo. Uma camada publicada sem isso volta a ser o que era: uma
        // que responde "estou carregada" para qualquer imóvel do país.
        await new AbrangenciaDeCamada(_context).DefinirAsync(camada, biomas, cancellationToken);

        // Medido, não declarado: o menor ano presente É o começo da cobertura,
        // e medir fecha a porta para a declaração divergir do arquivo.
        camada.CobreDesdeAno = menorAno == int.MaxValue ? null : menorAno;

        var publicacao = await troca.PublicarAsync(camada, gravados, cancellationToken);

        if (!publicacao.Aceita)
        {
            throw new InvalidOperationException(publicacao.Explicacao);
        }

        if (ilegiveis.Total > 0)
        {
            _logger.LogWarning(
                "Camada {Chave}: {Ilegiveis} leitura(s) falharam e foram puladas — o arquivo de " +
                "origem tem geometria corrompida. Conte pela diferença entre o total da origem e " +
                "o gravado para saber quantas feições de fato se perderam.",
                chave, ilegiveis.Total);
        }

        _logger.LogInformation(
            "Camada {Chave}: {Gravados} feições gravadas, {Descartados} descartadas de {Lidos} lidas.",
            chave, gravados, descartados, lidos);

        return new ResultadoImportacaoCamada(camada.Id, chave, lidos, gravados, descartados);
    }

    /// <summary>
    /// Percorre o shapefile pulando as feições que o leitor não consegue ler.
    /// </summary>
    /// <remarks>
    /// Sem isto, um único polígono corrompido derruba a camada inteira: a
    /// exceção sobe de dentro do enumerador e nada do que já tinha sido lido
    /// chega ao banco. Aconteceu de verdade com o arquivo de terras indígenas
    /// do IBGE — 573 polígonos perdidos por causa de um.
    ///
    /// Pular é melhor que abortar porque a camada parcial não passa batida: a
    /// conferência de tamanho recusa a carga se faltar muita coisa, e o que
    /// falta aparece no registro. Abortar, não — abortar deixa a camada velha
    /// sem ninguém saber por quê.
    /// </remarks>
    /// <summary>Contagem compartilhada: um iterador não aceita parâmetro por referência.</summary>
    private sealed class Contador
    {
        public int Total { get; set; }
    }

    private static IEnumerable<NetTopologySuite.Features.Feature> Legiveis(
        IEnumerable<NetTopologySuite.Features.Feature> leitor,
        Contador ilegiveis,
        CancellationToken cancellationToken)
    {
        // Teto de falhas seguidas: se o leitor parar de avançar, insistir viraria
        // laço infinito em vez de erro.
        const int FalhasSeguidasAceitas = 100;

        var enumerador = leitor.GetEnumerator();
        var seguidas = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NetTopologySuite.Features.Feature atual;

                try
                {
                    if (!enumerador.MoveNext())
                    {
                        break;
                    }

                    atual = enumerador.Current;
                    seguidas = 0;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ilegiveis.Total++;

                    if (++seguidas > FalhasSeguidasAceitas)
                    {
                        break;
                    }

                    continue;
                }

                yield return atual;
            }
        }
        finally
        {
            (enumerador as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Ano do fato que a feição registra, quando a origem o informa.
    /// </summary>
    /// <remarks>
    /// Aceita o ano direto e a data por extenso, porque as origens misturam os
    /// dois: o PRODES traz <c>year</c>, o MapBiomas traz <c>detected_at</c> e o
    /// DETER traz <c>view_date</c>. De uma data só interessa o ano — é a
    /// granularidade em que as regras cortam.
    ///
    /// Um ano fora de 1980..2100 é descartado em vez de aceito. Campo de ano
    /// vazio chega como zero em shapefile, e zero passaria em qualquer corte
    /// "a partir de 2008" pelo lado errado da comparação.
    /// </remarks>
    internal static int? ExtrairAnoDe(NetTopologySuite.Features.IAttributesTable? atributos)
    {
        if (atributos is null)
        {
            return null;
        }

        foreach (var campo in CamposAno)
        {
            if (!atributos.Exists(campo))
            {
                continue;
            }

            var valor = atributos[campo];

            var ano = valor switch
            {
                null => (int?)null,
                DateTime data => data.Year,
                int inteiro => inteiro,
                long longo => (int)longo,
                double real => (int)real,
                _ => AnoDeTexto(valor.ToString())
            };

            if (ano is >= 1980 and <= 2100)
            {
                return ano;
            }
        }

        return null;
    }

    /// <summary>Ano de um texto que é o próprio ano ou uma data que começa por ele.</summary>
    private static int? AnoDeTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.AsSpan().TrimStart();

        return inicio.Length >= 4 && int.TryParse(inicio[..4], out var ano) ? ano : null;
    }

    private static string? ExtrairRotulo(NetTopologySuite.Features.IAttributesTable atributos)
    {
        var nome = PrimeiroPreenchido(atributos, CamposRotulo);

        if (nome is null)
        {
            return null;
        }

        // A qualificação muda o peso do achado: uma terra indígena regularizada
        // e outra apenas em estudo restringem coisas diferentes, e quem lê o
        // laudo precisa distinguir sem ir atrás da base.
        var qualificador = PrimeiroPreenchido(atributos, CamposQualificador);

        var rotulo = qualificador is null ? nome : $"{nome} ({qualificador})";

        return rotulo.Length > 300 ? rotulo[..300] : rotulo;
    }

    private static string? PrimeiroPreenchido(
        NetTopologySuite.Features.IAttributesTable atributos, string[] candidatos)
    {
        var nomes = atributos.GetNames();

        foreach (var candidato in candidatos)
        {
            var nomeReal = nomes.FirstOrDefault(n => string.Equals(n, candidato, StringComparison.OrdinalIgnoreCase));

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

