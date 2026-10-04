using System.Globalization;
using System.Security.Cryptography;
using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoImportacao(
    long CargaId,
    int Lidos,
    int Gravados,
    int Descartados,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Carrega a camada AREA_IMOVEL do CAR (shapefile) para o PostGIS.
/// </summary>
/// <remarks>
/// O download do arquivo é feito por um operador: o portal do SICAR protege o
/// download com CAPTCHA e não expõe WFS nem API de download em massa. A camada
/// nacional consolidada publicada pelo MapBiomas ("CAR - Camada Completa") é a
/// entrada usual; o portal do SICAR serve para refresh por município.
/// </remarks>
public class SicarShapefileImporter
{
    private const int TamanhoLote = 2_000;

    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Fração de geometrias fora do Brasil a partir da qual a carga é recusada.
    /// </summary>
    /// <remarks>
    /// Imóvel solto fora da caixa é dado ruim na origem e vira descarte. Mas
    /// um arquivo inteiro fora dela não é dado ruim: é o arquivo errado, ou com
    /// latitude e longitude trocadas. Gravar isso não falha em lugar nenhum —
    /// só faz o cruzamento parar de encontrar coisa alguma, e todo laudo sair
    /// limpo.
    /// </remarks>
    private const double FracaoForaDoBrasilAceita = 0.10;

    private static readonly string[] CamposCodigo = { "COD_IMOVEL", "CAR", "COD_CAR", "CODIGO_CAR", "CAR_ID" };
    private static readonly string[] CamposArea = { "NUM_AREA", "AREA_HA", "AREA", "AREA_IMOVE" };
    private static readonly string[] CamposMunicipio = { "MUNICIPIO", "NOM_MUNICI", "NM_MUNICIP", "NOME_MUNIC" };
    private static readonly string[] CamposUf = { "COD_ESTADO", "UF", "SIGLA_UF", "ESTADO" };
    /// <summary>
    /// Situação do cadastro, na ordem de preferência.
    /// </summary>
    /// <remarks>
    /// "condicao" vem primeiro porque é a que diz algo: "Analisado, em
    /// conformidade", "Aguardando análise", "Cancelado por decisão
    /// administrativa". O status_imovel do SICAR é a sigla equivalente (AT, CA,
    /// PE, SU) e serve de reserva quando a origem não traz a condição por
    /// extenso.
    ///
    /// Importa que o cancelado entre: ele é cerca de 6% da base, e descartá-lo
    /// faria a análise responder "imóvel não está na base do CAR" para um
    /// imóvel que está lá e teve o cadastro anulado — duas situações
    /// diferentes que o laudo não pode confundir.
    /// </remarks>
    private static readonly string[] CamposSituacao =
    {
        "CONDICAO", "DES_CONDIC", "IND_STATUS", "SITUACAO", "STATUS", "STATUS_IMO", "STATUS_IMOVEL"
    };

    private static readonly string[] CamposTipo = { "IND_TIPO", "TIPO_IMOVE", "TIPO_IMOVEL", "TIPO" };

    /// <summary>
    /// Destino lógico → nomes de coluna aceitos. A comparação é case-insensitive,
    /// porque cada origem (SICAR, MapBiomas, SEMAs estaduais) escreve de um jeito.
    /// Exposto para que o comando "inspecionar" mostre o que é reconhecido.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> MapeamentoDeCampos =
        new Dictionary<string, string[]>
        {
            ["codigo_car"] = CamposCodigo,
            ["area_ha"] = CamposArea,
            ["municipio"] = CamposMunicipio,
            ["uf"] = CamposUf,
            ["situacao"] = CamposSituacao,
            ["tipo"] = CamposTipo
        };

    private readonly GeoDbContext _context;
    private readonly ILogger<SicarShapefileImporter> _logger;

    public SicarShapefileImporter(GeoDbContext context, ILogger<SicarShapefileImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <param name="pertenceAoEscopo">
    /// Decide, pelo código do IBGE, se um município está entre os que esta
    /// carga se propõe a cobrir por inteiro. Nulo quando a carga é de um
    /// arquivo completo, e aí tudo o que aparecer conta.
    /// </param>
    public async Task<ResultadoImportacao> ImportarAsync(
        string caminhoShapefile,
        string origem,
        string? uf = null,
        Func<string, bool>? pertenceAoEscopo = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoShapefile))
        {
            throw new FileNotFoundException("Shapefile não encontrado.", caminhoShapefile);
        }

        var carga = new CargaBaseCar
        {
            Origem = origem,
            Arquivo = Path.GetFileName(caminhoShapefile),
            HashArquivo = await CalcularHashAsync(caminhoShapefile, cancellationToken),
            Uf = uf
        };

        _context.Cargas.Add(carga);
        await _context.SaveChangesAsync(cancellationToken);

        var avisos = new List<string>();
        var lote = new List<ImovelCar>(TamanhoLote);

        // Municípios efetivamente vistos no arquivo. É daqui que sai a cobertura:
        // deduzi-la da existência de imóveis confundiria município sem cadastro
        // com município nunca carregado.
        var municipiosVistos = new Dictionary<string, (string Uf, string? Nome, int Imoveis)>();
        var foraDoBrasil = new Contador();

        try
        {
            // OpenRead e não ReadAllFeatures: este devolve Feature[], materializando
            // o arquivo inteiro. Medido em 224 MB para 48 mil feições — a base
            // nacional do CAR, com milhões de imóveis, não caberia na memória.
            // Em fluxo o mesmo arquivo custa 5 MB.
            using var leitor = Shapefile.OpenRead(
                caminhoShapefile,
                new ShapefileReaderOptions { Encoding = CodificacaoDeShapefile.Detectar(caminhoShapefile) });

            foreach (var feature in leitor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                carga.RegistrosLidos++;

                var imovel = Mapear(feature.Geometry, feature.Attributes, carga.Id, avisos, foraDoBrasil);

                if (imovel is null)
                {
                    carga.RegistrosDescartados++;
                    continue;
                }

                lote.Add(imovel);
                ContabilizarMunicipio(municipiosVistos, imovel);

                if (lote.Count >= TamanhoLote)
                {
                    carga.RegistrosGravados += await GravarLoteAsync(lote, cancellationToken);
                    lote.Clear();
                }
            }

            if (lote.Count > 0)
            {
                carga.RegistrosGravados += await GravarLoteAsync(lote, cancellationToken);
            }

            // Antes de registrar cobertura: cobertura é a afirmação de que o
            // município foi carregado, e não se pode afirmar isso sobre um
            // arquivo que caiu no oceano.
            ConferirSeEhDoBrasil(carga.RegistrosLidos, foraDoBrasil.Total);

            await RegistrarCoberturaAsync(
                municipiosVistos, pertenceAoEscopo, carga.Id, cancellationToken);

            carga.Status = StatusCarga.Concluida;
            carga.ConcluidaEm = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            carga.Status = StatusCarga.Falhou;
            carga.ConcluidaEm = DateTime.UtcNow;
            carga.Erro = ex.Message;
            _logger.LogError(ex, "Falha ao importar {Arquivo}", caminhoShapefile);

            _context.Cargas.Update(carga);
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        _context.Cargas.Update(carga);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Carga {CargaId} concluída: {Gravados} gravados, {Descartados} descartados de {Lidos} lidos.",
            carga.Id, carga.RegistrosGravados, carga.RegistrosDescartados, carga.RegistrosLidos);

        return new ResultadoImportacao(
            carga.Id, carga.RegistrosLidos, carga.RegistrosGravados, carga.RegistrosDescartados, avisos);
    }

    private static ImovelCar? Mapear(
        Geometry? geometria,
        NetTopologySuite.Features.IAttributesTable atributos,
        long cargaId,
        List<string> avisos,
        Contador foraDoBrasil)
    {
        var codigo = CodigoCar.Normalizar(Texto(atributos, CamposCodigo));

        if (codigo is null)
        {
            return null;
        }

        if (geometria is null || geometria.IsEmpty)
        {
            avisos.Add($"{codigo}: geometria ausente.");
            return null;
        }

        if (!geometria.IsValid)
        {
            // Auto-interseção é comum nos perímetros declarados; o buffer(0) resolve
            // a maioria sem alterar a área de forma relevante.
            geometria = geometria.Buffer(0);

            if (geometria.IsEmpty || !geometria.IsValid)
            {
                avisos.Add($"{codigo}: geometria inválida e não recuperável.");
                return null;
            }

            avisos.Add($"{codigo}: geometria corrigida por buffer(0).");
        }

        // Fora do Brasil é descarte, e o motivo mais provável não é dado ruim:
        // é latitude e longitude trocadas na origem. Um imóvel no meio do
        // Atlântico não cruza com nada, e sem este descarte a análise diria
        // "nenhuma sobreposição" com toda a confiança.
        if (!Geometrias.DentroDoBrasil(geometria))
        {
            foraDoBrasil.Total++;
            avisos.Add($"{codigo}: geometria fora do Brasil.");
            return null;
        }

        geometria.SRID = GeoDbContext.Srid;
        var centroide = geometria.Centroid;
        centroide.SRID = GeoDbContext.Srid;

        return new ImovelCar
        {
            CodigoCar = codigo,
            Perimetro = geometria,
            Centroide = centroide,
            AreaHa = Numero(atributos, CamposArea),
            Municipio = Texto(atributos, CamposMunicipio),
            Uf = CodigoCar.ExtrairUf(codigo) ?? Texto(atributos, CamposUf),
            CodigoIbge = CodigoCar.ExtrairCodigoIbge(codigo),
            // Cortado no tamanho da coluna porque vem de fora: uma origem
            // estadual com texto mais longo derrubaria a carga inteira, e
            // perder o fim de uma frase é melhor que perder o município.
            Situacao = Limitar(Texto(atributos, CamposSituacao), 120),
            Tipo = Limitar(Texto(atributos, CamposTipo), 20),
            CargaId = cargaId
        };
    }

    private static void ContabilizarMunicipio(
        Dictionary<string, (string Uf, string? Nome, int Imoveis)> municipios,
        ImovelCar imovel)
    {
        if (string.IsNullOrWhiteSpace(imovel.CodigoIbge))
        {
            return;
        }

        if (municipios.TryGetValue(imovel.CodigoIbge, out var atual))
        {
            municipios[imovel.CodigoIbge] = (atual.Uf, atual.Nome ?? imovel.Municipio, atual.Imoveis + 1);
            return;
        }

        municipios[imovel.CodigoIbge] = (imovel.Uf ?? string.Empty, imovel.Municipio, 1);
    }

    /// <summary>
    /// Marca como cobertos os municípios vistos no arquivo e limpa as lacunas
    /// correspondentes — o que era pedido e não existia agora existe.
    /// </summary>
    private async Task RegistrarCoberturaAsync(
        Dictionary<string, (string Uf, string? Nome, int Imoveis)> municipios,
        Func<string, bool>? pertenceAoEscopo,
        long cargaId,
        CancellationToken cancellationToken)
    {
        // Aparecer no arquivo não é o mesmo que estar coberto. O SICAR tem
        // registros em que o cod_municipio_ibge e o município embutido no
        // código do CAR discordam, então uma carga filtrada traz junto imóveis
        // de fora do que se pediu — numa carga de Goiás apareceram dois imóveis
        // com código do Distrito Federal.
        //
        // Eles são gravados, porque são dados reais. Mas o município deles não
        // pode ser dado por coberto: uma consulta lá deixaria de virar lacuna, e
        // ninguém saberia que o município inteiro ainda falta. Dois imóveis
        // registrando o Distrito Federal como coberto é a forma mais barata de
        // esconder um estado inteiro.
        if (pertenceAoEscopo is not null)
        {
            municipios = municipios
                .Where(x => pertenceAoEscopo(x.Key))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        if (municipios.Count == 0)
        {
            return;
        }

        var codigos = municipios.Keys.ToList();

        // A contagem sai do banco, e não do arquivo que acabou de ser lido.
        // Uma carga por páginas chama este método uma vez por página, e um
        // município grande aparece em mais de uma: contar o que veio na página
        // faria a última sobrescrever as anteriores, e o número exibido ficaria
        // menor que a realidade sem nada indicar isso.
        var totaisReais = await _context.ImoveisCar
            .Where(x => x.CodigoIbge != null && codigos.Contains(x.CodigoIbge))
            .GroupBy(x => x.CodigoIbge!)
            .Select(x => new { Codigo = x.Key, Total = x.Count() })
            .ToDictionaryAsync(x => x.Codigo, x => x.Total, cancellationToken);

        var existentes = await _context.Cobertura
            .Where(x => codigos.Contains(x.CodigoIbge))
            .ToDictionaryAsync(x => x.CodigoIbge, cancellationToken);

        foreach (var (codigoIbge, dados) in municipios)
        {
            if (existentes.TryGetValue(codigoIbge, out var cobertura))
            {
                cobertura.Uf = dados.Uf;
                cobertura.Municipio = dados.Nome ?? cobertura.Municipio;
                cobertura.Imoveis = totaisReais.GetValueOrDefault(codigoIbge, dados.Imoveis);
                cobertura.CargaId = cargaId;
                cobertura.CobertoEm = DateTime.UtcNow;
                continue;
            }

            _context.Cobertura.Add(new CoberturaMunicipio
            {
                CodigoIbge = codigoIbge,
                Uf = dados.Uf,
                Municipio = dados.Nome,
                Imoveis = totaisReais.GetValueOrDefault(codigoIbge, dados.Imoveis),
                CargaId = cargaId,
                CobertoEm = DateTime.UtcNow
            });
        }

        var lacunasResolvidas = await _context.Lacunas
            .Where(x => codigos.Contains(x.CodigoIbge))
            .ToListAsync(cancellationToken);

        _context.Lacunas.RemoveRange(lacunasResolvidas);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cobertura atualizada: {Municipios} município(s); {Lacunas} lacuna(s) resolvida(s).",
            municipios.Count, lacunasResolvidas.Count);
    }

    /// <summary>
    /// Grava o lote com upsert por código do CAR: recarregar a base é operação
    /// rotineira e não pode falhar por chave duplicada.
    /// </summary>
    private async Task<int> GravarLoteAsync(List<ImovelCar> lote, CancellationToken cancellationToken)
    {
        var codigos = lote.Select(x => x.CodigoCar).ToList();

        var existentes = await _context.ImoveisCar
            .Where(x => codigos.Contains(x.CodigoCar))
            .ToDictionaryAsync(x => x.CodigoCar, cancellationToken);

        foreach (var imovel in lote)
        {
            if (existentes.TryGetValue(imovel.CodigoCar, out var atual))
            {
                atual.Perimetro = imovel.Perimetro;
                atual.Centroide = imovel.Centroide;
                atual.AreaHa = imovel.AreaHa;
                atual.Municipio = imovel.Municipio;
                atual.Uf = imovel.Uf;
                atual.CodigoIbge = imovel.CodigoIbge;
                atual.Situacao = imovel.Situacao;
                atual.Tipo = imovel.Tipo;
                atual.CargaId = imovel.CargaId;
                continue;
            }

            _context.ImoveisCar.Add(imovel);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();

        return lote.Count;
    }

    private static string? Texto(NetTopologySuite.Features.IAttributesTable atributos, string[] candidatos)
    {
        var nomesReais = atributos.GetNames();

        foreach (var candidato in candidatos)
        {
            // Comparação case-insensitive: o SICAR grava em maiúsculas e outras
            // origens em minúsculas, para a mesma coluna.
            var nomeReal = nomesReais.FirstOrDefault(n => string.Equals(n, candidato, StringComparison.OrdinalIgnoreCase));

            if (nomeReal is null)
            {
                continue;
            }

            var valor = atributos[nomeReal]?.ToString();

            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor.Trim();
            }
        }

        return null;
    }

    /// <summary>Contagem compartilhada entre o laço e o mapeamento.</summary>
    private sealed class Contador
    {
        public int Total { get; set; }
    }

    /// <summary>
    /// Recusa o arquivo quando quase tudo nele cai fora do Brasil.
    /// </summary>
    /// <remarks>
    /// Um imóvel fora da caixa é dado ruim na origem. O arquivo inteiro fora
    /// dela é outra coisa: é o arquivo errado, ou com latitude e longitude
    /// trocadas — foi o que aconteceu com o SHAPE-ZIP do SICAR em WFS 1.1.0.
    /// Gravar isso não falha em lugar nenhum, só faz o cruzamento parar de
    /// encontrar qualquer coisa. Melhor a carga morrer alto.
    /// </remarks>
    private static void ConferirSeEhDoBrasil(int lidos, int foraDoBrasil)
    {
        if (lidos == 0 || foraDoBrasil < lidos * FracaoForaDoBrasilAceita)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{foraDoBrasil:N0} de {lidos:N0} geometrias caem fora do Brasil. " +
            "Suspeite de latitude e longitude trocadas na origem, ou de sistema de " +
            "coordenadas diferente do esperado. Nenhum imóvel foi publicado.");
    }

    private static string? Limitar(string? texto, int tamanho) =>
        texto is not null && texto.Length > tamanho ? texto[..tamanho] : texto;

    /// <summary>Valor cru do primeiro campo encontrado, sem conversão para texto.</summary>
    private static object? Bruto(NetTopologySuite.Features.IAttributesTable atributos, string[] candidatos)
    {
        var nomesReais = atributos.GetNames();

        foreach (var candidato in candidatos)
        {
            var nomeReal = nomesReais.FirstOrDefault(n => string.Equals(n, candidato, StringComparison.OrdinalIgnoreCase));

            if (nomeReal is not null && atributos[nomeReal] is { } valor)
            {
                return valor;
            }
        }

        return null;
    }

    private static double? Numero(NetTopologySuite.Features.IAttributesTable atributos, string[] candidatos)
    {
        return ConverterParaDouble(Bruto(atributos, candidatos));
    }

    /// <summary>
    /// Converte o valor cru de um campo do .dbf para double.
    /// </summary>
    /// <remarks>
    /// Campos numéricos chegam como double/decimal e não podem passar por
    /// ToString(), que aplica a cultura corrente e produz "1234,5" — texto que
    /// o parser invariante rejeita, zerando a área de todos os registros.
    /// Campos de texto podem vir com vírgula ou com ponto, conforme a origem.
    /// </remarks>
    public static double? ConverterParaDouble(object? bruto)
    {
        switch (bruto)
        {
            case null:
                return null;
            case double d:
                return d;
            case float f:
                return f;
            case decimal m:
                return (double)m;
            case int i:
                return i;
            case long l:
                return l;
        }

        var texto = bruto.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var normalizado = NormalizarSeparadores(texto);

        // NumberStyles.Float, e não Any: com AllowThousands o .NET aceita
        // agrupamento em qualquer posição, e "1234,5" viraria 12345 em silêncio.
        return double.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    /// <summary>
    /// Reduz um número escrito em qualquer convenção para a forma invariante.
    /// </summary>
    /// <remarks>
    /// Regras, nesta ordem:
    /// "1.234,56" ou "1,234.56" → o separador que aparece por último é o decimal;
    /// só vírgulas → vírgula é decimal (as origens são brasileiras, que escrevem
    /// milhar com ponto); só pontos, mais de um → todos são separadores de milhar.
    /// </remarks>
    private static string NormalizarSeparadores(string texto)
    {
        var limpo = new string(texto.Where(c => !char.IsWhiteSpace(c)).ToArray());

        var ultimaVirgula = limpo.LastIndexOf(',');
        var ultimoPonto = limpo.LastIndexOf('.');

        if (ultimaVirgula >= 0 && ultimoPonto >= 0)
        {
            var decimalEhVirgula = ultimaVirgula > ultimoPonto;
            var milhar = decimalEhVirgula ? '.' : ',';
            var separador = decimalEhVirgula ? ',' : '.';

            return new string(limpo.Where(c => c != milhar).ToArray()).Replace(separador, '.');
        }

        if (ultimaVirgula >= 0)
        {
            return limpo.Replace(',', '.');
        }

        // Mais de um ponto só pode ser agrupamento de milhar: "1.234.567".
        if (limpo.Count(c => c == '.') > 1)
        {
            return limpo.Replace(".", string.Empty);
        }

        return limpo;
    }

    private static async Task<string> CalcularHashAsync(string caminho, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(caminho);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
