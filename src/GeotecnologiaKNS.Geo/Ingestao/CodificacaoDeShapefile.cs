using System.Text;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Descobre em que codificação os textos de um shapefile estão.
/// </summary>
/// <remarks>
/// A biblioteca lê o arquivo <c>.cpg</c>/<c>.cst</c> ao lado do shapefile e
/// honra o que ele diz. Quando ele não existe, porém, ela assume UTF-8 — e os
/// órgãos brasileiros publicam em Latin1.
///
/// O estrago aparece no laudo: o assentamento "PA PROVIDÊNCIA" do INCRA virava
/// "PA PROVID?NCIA", porque o byte 0xCA do Latin1 não é UTF-8 válido. É defeito
/// pequeno e visível, ao contrário dos outros desta base — mas sai impresso num
/// documento que a indústria entrega a auditoria.
///
/// A ordem aqui é a mesma que um humano usaria: acredite no que o arquivo
/// declara; se ele não declarar nada, use o cabeçalho do DBF; e só então
/// recorra ao que a origem costuma ser.
/// </remarks>
public static class CodificacaoDeShapefile
{
    private static readonly string[] Declaracoes = { ".cpg", ".cst", ".CPG", ".CST" };

    /// <summary>
    /// Codificação a usar, ou nulo quando o próprio arquivo já declara a dele.
    /// </summary>
    public static Encoding? Detectar(string caminhoShapefile)
    {
        // Declarou: a biblioteca lê o .cpg/.cst sozinha e acerta. Devolver
        // nulo aqui é o que preserva isso — o SICAR declara ISO-8859-1 e vem
        // correto sem ajuda nenhuma.
        if (Declaracoes.Any(extensao => File.Exists(Path.ChangeExtension(caminhoShapefile, extensao))))
        {
            return null;
        }

        return DoCabecalhoDbf(caminhoShapefile) ?? Encoding.Latin1;
    }

    /// <summary>
    /// Codificação declarada no cabeçalho do DBF, quando houver.
    /// </summary>
    /// <remarks>
    /// O byte 29 do cabeçalho é o "language driver id" do dBASE. O arquivo do
    /// INCRA traz 0x57, que é ANSI — e a biblioteca não olha esse byte quando
    /// falta o .cpg.
    /// </remarks>
    private static Encoding? DoCabecalhoDbf(string caminhoShapefile)
    {
        var dbf = Path.ChangeExtension(caminhoShapefile, ".dbf");

        if (!File.Exists(dbf))
        {
            dbf = Path.ChangeExtension(caminhoShapefile, ".DBF");

            if (!File.Exists(dbf))
            {
                return null;
            }
        }

        try
        {
            using var fluxo = File.OpenRead(dbf);

            if (fluxo.Length <= 29)
            {
                return null;
            }

            fluxo.Seek(29, SeekOrigin.Begin);

            var identificador = (byte)fluxo.ReadByte();

            return Mapear(identificador);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Traduz o "language driver id" do dBASE em codificação.
    /// </summary>
    /// <remarks>
    /// Só a família ANSI está aqui, e de propósito. Ela cobre tudo o que as
    /// origens em uso publicam — o INCRA declara 0x57 — e mapeia para Latin1,
    /// que difere do Windows-1252 apenas na faixa 0x80–0x9F, onde texto
    /// acentuado em português não cai.
    ///
    /// As páginas de código do MS-DOS (437, 850 e companhia) ficaram de fora
    /// porque o .NET não as traz embutidas: precisariam do pacote
    /// System.Text.Encoding.CodePages. Nenhuma origem usada hoje as declara, e
    /// acrescentar dependência por um caso que não existe seria pagar adiantado.
    /// Se aparecer uma, o sintoma será acento errado no laudo e a correção é
    /// este método.
    /// </remarks>
    private static Encoding? Mapear(byte identificador) => identificador switch
    {
        // 0x00 é "não declarado", e é o valor mais comum.
        0x00 => null,

        // ANSI, em suas variantes regionais.
        0x03 or 0x57 or 0x58 or 0x59 => Encoding.Latin1,

        _ => null
    };
}
