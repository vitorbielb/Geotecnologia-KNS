namespace GeotecnologiaKNS.Geo.Ingestao;

/// <summary>
/// Motivo pelo qual uma recarga foi recusada.
/// </summary>
public enum MotivoDaRecusa
{
    Nenhum = 0,

    /// <summary>A carga não produziu registro algum.</summary>
    Vazia = 1,

    /// <summary>A carga perdeu parte grande do que havia antes.</summary>
    EncolheuDemais = 2
}

public record ResultadoDaTroca(
    bool Aceita,
    MotivoDaRecusa Motivo,
    int Antes,
    int Depois,
    string Unidade = "feições")
{
    public string Explicacao => Motivo switch
    {
        MotivoDaRecusa.Vazia =>
            $"A carga não produziu {Unidade} alguma. A versão anterior foi mantida.",

        MotivoDaRecusa.EncolheuDemais =>
            $"A carga trouxe {Formatos.Quantidade(Depois)} {Unidade} contra " +
            $"{Formatos.Quantidade(Antes)} da versão " +
            "anterior. Uma queda dessa ordem costuma ser arquivo truncado na origem, " +
            "não redução real. A versão anterior foi mantida.",

        _ => $"{Formatos.Quantidade(Depois)} {Unidade} ({Formatos.Quantidade(Antes)} antes)."
    };
}

/// <summary>
/// A conferência que decide se uma carga nova pode substituir a que está no ar.
/// </summary>
/// <remarks>
/// Vale igualmente para camada geográfica e para lista restritiva por documento,
/// porque o estrago é o mesmo: se a origem publicar um arquivo truncado e a
/// carga for aceita sem conferência, o sistema passa a liberar o que deveria
/// bloquear — e, numa recarga desassistida, ninguém percebe até alguém
/// contestar um laudo.
/// </remarks>
public static class GuardaDeCarga
{
    /// <summary>
    /// Fração do tamanho anterior abaixo da qual a carga é recusada.
    /// </summary>
    /// <remarks>
    /// Metade é folgado de propósito. Base de referência não encolhe pela
    /// metade de uma semana para outra por motivo legítimo: embargo revogado e
    /// unidade de conservação extinta são dezenas, não dezenas de milhares.
    /// Recusar e manter o que havia é sempre mais seguro que aceitar e liberar
    /// fornecedor que não foi verificado.
    /// </remarks>
    public const double FracaoMinima = 0.5;

    /// <summary>
    /// Margem aceita entre o que a origem anuncia e o que chegou.
    /// </summary>
    /// <remarks>
    /// Não é zero porque a contagem e o download são duas requisições: uma
    /// feição publicada entre as duas aparece como diferença e não é defeito.
    /// Um por cento absorve isso e ainda assim denuncia truncamento — truncar
    /// corta dezenas de por cento, não frações.
    /// </remarks>
    public const double MargemDaOrigem = 0.01;

    /// <summary>
    /// Confere o que chegou contra o que a origem declarou ter.
    /// </summary>
    /// <remarks>
    /// Existe por causa do caso mais caro desta base. O GeoServer do INPE
    /// limita cada requisição a 50.000 feições e, acima disso, devolve as
    /// primeiras 50.000 com <b>200 OK</b>. Nada falha: o zip é válido, o
    /// shapefile abre, a carga é gravada e publicada. O PRODES da Amazônia
    /// entrou no ar com 50.000 polígonos de 802.277 — e a conferência de
    /// encolhimento não viu nada, porque não havia versão anterior com que
    /// comparar.
    ///
    /// Esta aqui compara com quem sabe a resposta: a própria origem. Ela pega
    /// o truncamento na primeira carga, que é justamente quando a outra
    /// conferência é cega.
    ///
    /// Lança em vez de devolver motivo porque acontece antes da troca
    /// versionada: a exceção sobe, a versão nova é descartada e a anterior
    /// continua no ar.
    /// </remarks>
    public static void ConferirContraOrigem(string chave, int? esperado, int lidos)
    {
        if (esperado is not int total || total <= 0)
        {
            return;
        }

        var minimo = (int)(total * (1 - MargemDaOrigem));

        if (lidos >= minimo)
        {
            return;
        }

        throw new InvalidOperationException(
            $"A origem de {chave} declara {Formatos.Quantidade(total)} feições e a carga leu " +
            $"{Formatos.Quantidade(lidos)}. Falta demais para ser diferença de publicação: " +
            "ou a resposta veio truncada por limite do servidor — e aí a camada precisa ser " +
            "baixada em páginas —, ou o download foi cortado no meio. A versão anterior foi " +
            "mantida, porque publicar o pedaço faria a análise liberar o que não verificou.");
    }

    public static MotivoDaRecusa Avaliar(int antes, int depois)
    {
        if (depois == 0)
        {
            return MotivoDaRecusa.Vazia;
        }

        // Primeira carga não tem com o que comparar.
        if (antes == 0)
        {
            return MotivoDaRecusa.Nenhum;
        }

        return depois < antes * FracaoMinima
            ? MotivoDaRecusa.EncolheuDemais
            : MotivoDaRecusa.Nenhum;
    }
}
