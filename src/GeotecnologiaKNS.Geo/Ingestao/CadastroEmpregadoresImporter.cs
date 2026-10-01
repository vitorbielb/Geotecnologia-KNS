using System.Text;
using System.Text.RegularExpressions;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeotecnologiaKNS.Geo.Ingestao;

public record ResultadoImportacaoCadastro(
    int Linhas,
    int Registros,
    int Gravados,
    int SemDocumento);

/// <summary>
/// Carrega o Cadastro de Empregadores — a chamada "lista suja" do trabalho
/// análogo à escravidão, publicada pelo Ministério do Trabalho e Emprego.
/// </summary>
/// <remarks>
/// O MTE publica só em PDF. Não há CSV, JSON nem API: tentei as quatro
/// extensões no mesmo caminho e todas devolvem 403 — só o .pdf responde.
///
/// Por isso a entrada aqui é o texto já extraído, não o PDF. Embutir um leitor
/// de PDF no sistema para um arquivo que muda de layout a cada portaria traria
/// uma dependência pesada para um ganho duvidoso; `pdftotext -layout` resolve
/// num passo que o operador faz junto com o download, e está documentado.
/// </remarks>
public partial class CadastroEmpregadoresImporter
{
    private const int TamanhoLote = 500;

    public const string Origem = "MTE — Cadastro de Empregadores";

    /// <summary>
    /// A portaria manda atualizar o cadastro semestralmente, mas a publicação
    /// na prática sai a cada poucos meses; conferir mensalmente custa um PDF.
    /// </summary>
    public const int PeriodicidadeDias = 30;

    /// <summary>
    /// Início de um registro: número sequencial, ano da ação fiscal e UF.
    /// </summary>
    /// <remarks>
    /// É o que distingue uma linha de registro das linhas de cabeçalho que se
    /// repetem a cada página do PDF. O registro pode continuar nas linhas
    /// seguintes quando o endereço é longo, e por isso tudo até o próximo
    /// início é tratado como parte do mesmo.
    /// </remarks>
    [GeneratedRegex(@"^\s*(?<id>\d+)\s+(?<ano>(?:19|20)\d{2})\s+(?<uf>[A-Z]{2})\s+(?<resto>.*)$")]
    private static partial Regex InicioDeRegistro();

    [GeneratedRegex(@"\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}|\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex Documento();

    /// <summary>Município e UF no fim do endereço do estabelecimento.</summary>
    [GeneratedRegex(@"([A-ZÀ-Ú][A-Za-zÀ-ú'´`\. ]{2,40})\s*/\s*([A-Z]{2})")]
    private static partial Regex MunicipioUf();

    [GeneratedRegex(@"\d{2}/\d{2}/\d{4}")]
    private static partial Regex Data();

    private readonly GeoDbContext _context;
    private readonly ILogger<CadastroEmpregadoresImporter> _logger;

    public CadastroEmpregadoresImporter(GeoDbContext context, ILogger<CadastroEmpregadoresImporter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ResultadoImportacaoCadastro> ImportarAsync(
        string caminhoTexto,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(caminhoTexto))
        {
            throw new FileNotFoundException("Arquivo do cadastro não encontrado.", caminhoTexto);
        }

        // Recarga substitui a lista inteira: nome que saiu do cadastro — por
        // decisão judicial ou pelo decurso dos dois anos — não pode continuar
        // bloqueando quem já se regularizou. A lista velha só é descartada
        // depois que a nova for conferida, senão um PDF truncado na origem
        // apagaria a verificação e a regra TRB-001 passaria a liberar todo mundo
        // enquanto continua se declarando avaliada.
        var troca = new TrocaDeListaRestritiva(_context, _logger);

        var lista = await troca.PrepararAsync(
            TipoRestricao.TrabalhoEscravo,
            "Cadastro de Empregadores",
            Origem,
            PeriodicidadeDias,
            cancellationToken);

        var versao = TrocaDeListaRestritiva.ProximaVersao(lista);

        var linhas = 0;
        var registros = 0;
        var semDocumento = 0;
        var gravados = 0;
        var lote = new List<RestricaoDocumento>(TamanhoLote);

        var atual = new StringBuilder();
        var nomeCompleto = new StringBuilder();
        var colunaDoDocumento = 0;

        foreach (var linha in File.ReadLines(caminhoTexto, Encoding.UTF8))
        {
            cancellationToken.ThrowIfCancellationRequested();
            linhas++;

            if (!InicioDeRegistro().IsMatch(linha))
            {
                // Continuação do registro anterior, ou cabeçalho de página.
                if (atual.Length > 0)
                {
                    atual.Append(' ').Append(linha.Trim());

                    // O nome do empregador quebra em mais de uma linha quando é
                    // longo. No modo tabela as colunas ficam alinhadas, então o
                    // que estiver antes da coluna do documento é continuação do
                    // nome — e o que vier depois é continuação do endereço.
                    if (colunaDoDocumento > 0 && linha.Length > 0)
                    {
                        var ateAColuna = linha[..Math.Min(colunaDoDocumento, linha.Length)].Trim();

                        if (ateAColuna.Length > 0)
                        {
                            nomeCompleto.Append(' ').Append(ateAColuna);
                        }
                    }
                }

                continue;
            }

            registros += Fechar(atual, nomeCompleto, lote, versao, ref semDocumento);
            atual.Clear();
            nomeCompleto.Clear();
            atual.Append(linha.Trim());

            var documentoNaLinha = Documento().Match(linha);
            colunaDoDocumento = documentoNaLinha.Success ? documentoNaLinha.Index : 0;

            if (lote.Count >= TamanhoLote)
            {
                gravados += await GravarLoteAsync(lote, cancellationToken);
                lote.Clear();
            }
        }

        registros += Fechar(atual, nomeCompleto, lote, versao, ref semDocumento);

        if (lote.Count > 0)
        {
            gravados += await GravarLoteAsync(lote, cancellationToken);
        }

        var publicacao = await troca.PublicarAsync(lista, gravados, cancellationToken);

        if (!publicacao.Aceita)
        {
            throw new InvalidOperationException(publicacao.Explicacao);
        }

        _logger.LogInformation(
            "Cadastro de Empregadores: {Gravados} documentos gravados de {Registros} registros " +
            "em {Linhas} linhas ({SemDocumento} sem documento legível).",
            gravados, registros, linhas, semDocumento);

        return new ResultadoImportacaoCadastro(linhas, registros, gravados, semDocumento);
    }

    /// <summary>
    /// Transforma o registro acumulado em restrição. Devolve 1 se havia
    /// registro, 0 se não.
    /// </summary>
    private static int Fechar(
        StringBuilder acumulado, StringBuilder continuacaoDoNome,
        List<RestricaoDocumento> lote, int versao, ref int semDocumento)
    {
        if (acumulado.Length == 0)
        {
            return 0;
        }

        var texto = acumulado.ToString();
        var inicio = InicioDeRegistro().Match(texto);

        if (!inicio.Success)
        {
            return 0;
        }

        var documento = Documento().Match(texto);

        if (!documento.Success)
        {
            // Acontece quando o PDF quebra o número no meio. Contado e
            // relatado: se esse número subir, o layout mudou e o importador
            // precisa de ajuste — melhor saber do que importar pela metade em
            // silêncio.
            semDocumento++;
            return 1;
        }

        var normalizado = RestricaoDocumentoService.Normalizar(documento.Value);

        if (normalizado is null)
        {
            semDocumento++;
            return 1;
        }

        var resto = inicio.Groups["resto"].Value;
        var nome = resto[..Math.Max(0, resto.IndexOf(documento.Value, StringComparison.Ordinal))].Trim();

        if (continuacaoDoNome.Length > 0)
        {
            nome = (nome + " " + continuacaoDoNome.ToString().Trim()).Trim();
        }

        lote.Add(new RestricaoDocumento
        {
            Documento = normalizado,
            NomeTitular = Limitar(nome, 250),
            Tipo = TipoRestricao.TrabalhoEscravo,
            Origem = Origem,
            Referencia = $"Ação fiscal {inicio.Groups["ano"].Value} — item {inicio.Groups["id"].Value}",
            Municipio = Limitar(ExtrairMunicipio(texto), 150),
            Uf = inicio.Groups["uf"].Value,
            DataRestricao = ExtrairInclusao(texto),
            TemGeometria = false,
            Versao = versao
        });

        return 1;
    }

    /// <summary>
    /// Município do estabelecimento, lido do fim do endereço.
    /// </summary>
    /// <remarks>
    /// O endereço é texto livre e nem sempre termina em "CIDADE/UF". Quando não
    /// dá para identificar, fica vazio — é informação de apoio no laudo, e
    /// inventar um município seria pior que não ter.
    /// </remarks>
    private static string? ExtrairMunicipio(string texto)
    {
        var achados = MunicipioUf().Matches(texto);

        return achados.Count == 0
            ? null
            : achados[^1].Groups[1].Value.Trim();
    }

    /// <summary>
    /// Data de inclusão no cadastro — a última das datas da linha.
    /// </summary>
    private static string? ExtrairInclusao(string texto)
    {
        var datas = Data().Matches(texto);
        return datas.Count == 0 ? null : datas[^1].Value;
    }

    private static string? Limitar(string? valor, int tamanho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return valor.Length <= tamanho ? valor : valor[..tamanho];
    }

    private async Task<int> GravarLoteAsync(
        List<RestricaoDocumento> lote, CancellationToken cancellationToken)
    {
        _context.RestricoesPorDocumento.AddRange(lote);
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();

        return lote.Count;
    }
}
