using System.Text;

namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Leitura em fluxo de CSV delimitado, com aspas e quebras de linha dentro do
/// campo.
/// </summary>
/// <remarks>
/// Escrito à mão em vez de String.Split porque os arquivos do IBAMA têm campos
/// livres — descrição da infração, localização — que contêm o próprio
/// delimitador e quebras de linha. Dividir por caractere desalinharia as
/// colunas silenciosamente, e a geometria acabaria lida de outro campo.
///
/// Em fluxo porque o arquivo de termos de embargo passa de 200 MB: carregá-lo
/// inteiro para dividir linhas gastaria memória à toa.
/// </remarks>
public sealed class LeitorCsv : IDisposable
{
    private readonly TextReader _leitor;
    private readonly char _delimitador;
    private bool _fim;

    public LeitorCsv(TextReader leitor, char delimitador = ';')
    {
        _leitor = leitor;
        _delimitador = delimitador;
    }

    /// <summary>Nomes das colunas, lidos do cabeçalho.</summary>
    public IReadOnlyList<string> Colunas { get; private set; } = Array.Empty<string>();

    public void LerCabecalho()
    {
        var cabecalho = LerRegistro();

        if (cabecalho is null)
        {
            throw new InvalidDataException("O arquivo está vazio.");
        }

        // O BOM entra colado no primeiro nome de coluna e faria a busca por
        // nome falhar justamente na primeira.
        Colunas = cabecalho.Select((c, i) => i == 0 ? c.TrimStart('﻿') : c).ToList();
    }

    public int IndiceDe(string coluna)
    {
        for (var i = 0; i < Colunas.Count; i++)
        {
            if (string.Equals(Colunas[i], coluna, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new InvalidDataException($"Coluna '{coluna}' não encontrada no arquivo.");
    }

    /// <summary>Próximo registro, ou null no fim do arquivo.</summary>
    public IReadOnlyList<string>? LerRegistro()
    {
        if (_fim)
        {
            return null;
        }

        var campos = new List<string>();
        var atual = new StringBuilder();
        var entreAspas = false;
        var vazio = true;

        while (true)
        {
            var lido = _leitor.Read();

            if (lido < 0)
            {
                _fim = true;

                if (vazio && campos.Count == 0 && atual.Length == 0)
                {
                    return null;
                }

                campos.Add(atual.ToString());
                return campos;
            }

            vazio = false;
            var c = (char)lido;

            if (entreAspas)
            {
                if (c != '"')
                {
                    atual.Append(c);
                    continue;
                }

                // Aspas dobradas representam uma aspa literal dentro do campo.
                if (_leitor.Peek() == '"')
                {
                    _leitor.Read();
                    atual.Append('"');
                    continue;
                }

                entreAspas = false;
                continue;
            }

            if (c == '"')
            {
                entreAspas = true;
                continue;
            }

            if (c == _delimitador)
            {
                campos.Add(atual.ToString());
                atual.Clear();
                continue;
            }

            if (c == '\r')
            {
                continue;
            }

            if (c == '\n')
            {
                campos.Add(atual.ToString());
                return campos;
            }

            atual.Append(c);
        }
    }

    public void Dispose() => _leitor.Dispose();
}
