using System.Text;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Percorre as feições de um FeatureCollection sem carregar o arquivo inteiro.
/// </summary>
/// <remarks>
/// O GeoJsonReader do NetTopologySuite recebe string: o arquivo de unidades de
/// conservação tem 227 MB, que viram cerca de 450 MB em memória só para
/// começar a ler. Aqui o arquivo é varrido byte a byte até o início do vetor
/// "features" e devolvido um objeto por vez, contando chaves para achar o fim
/// de cada um — a mesma razão pela qual a importação de shapefile usa leitura
/// em fluxo.
///
/// As aspas e o escape são respeitados na contagem: uma chave dentro de um
/// texto ("Parque Nacional {do} Something") desalinharia o balanço e partiria
/// a feição no meio.
/// </remarks>
public sealed class LeitorGeoJson : IDisposable
{
    private readonly TextReader _leitor;

    public LeitorGeoJson(TextReader leitor) => _leitor = leitor;

    /// <summary>
    /// Devolve cada feição como um trecho de JSON, pronta para o GeoJsonReader.
    /// </summary>
    public IEnumerable<string> LerFeicoes()
    {
        if (!PosicionarNoVetorDeFeicoes())
        {
            yield break;
        }

        while (true)
        {
            var feicao = LerProximoObjeto();

            if (feicao is null)
            {
                yield break;
            }

            yield return feicao;
        }
    }

    /// <summary>
    /// Avança até logo depois do colchete que abre "features".
    /// </summary>
    private bool PosicionarNoVetorDeFeicoes()
    {
        const string alvo = "\"features\"";
        var casados = 0;

        int lido;

        while ((lido = _leitor.Read()) >= 0)
        {
            var c = (char)lido;

            casados = c == alvo[casados] ? casados + 1 : (c == alvo[0] ? 1 : 0);

            if (casados != alvo.Length)
            {
                continue;
            }

            // Encontrado o nome do campo; agora até o colchete de abertura.
            while ((lido = _leitor.Read()) >= 0)
            {
                if ((char)lido == '[')
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Lê o próximo objeto JSON do vetor, ou null quando o vetor acaba.
    /// </summary>
    private string? LerProximoObjeto()
    {
        int lido;

        // Pula vírgulas e espaços entre as feições.
        while ((lido = _leitor.Read()) >= 0)
        {
            var c = (char)lido;

            if (c == '{')
            {
                break;
            }

            if (c == ']')
            {
                return null;
            }
        }

        if (lido < 0)
        {
            return null;
        }

        var texto = new StringBuilder("{");
        var profundidade = 1;
        var entreAspas = false;
        var escapado = false;

        while ((lido = _leitor.Read()) >= 0)
        {
            var c = (char)lido;
            texto.Append(c);

            if (escapado)
            {
                escapado = false;
                continue;
            }

            if (entreAspas)
            {
                if (c == '\\')
                {
                    escapado = true;
                }
                else if (c == '"')
                {
                    entreAspas = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    entreAspas = true;
                    break;

                case '{':
                    profundidade++;
                    break;

                case '}':
                    profundidade--;

                    if (profundidade == 0)
                    {
                        return texto.ToString();
                    }

                    break;
            }
        }

        // Arquivo truncado: melhor descartar a feição incompleta do que
        // entregar geometria pela metade.
        return null;
    }

    public void Dispose() => _leitor.Dispose();
}
