namespace GeotecnologiaKNS.Geo.Entities;

/// <summary>
/// Uma lista restritiva por documento, e o retrato da última carga dela.
/// </summary>
/// <remarks>
/// Existe pelo mesmo motivo que <see cref="CamadaReferencia.VersaoAtual"/>: a
/// carga grava a versão seguinte ao lado da que está no ar e só troca o número
/// no fim, depois de conferida.
///
/// Sem isso, recarregar os embargos do IBAMA apagava as cem mil restrições por
/// documento antes de saber se o arquivo novo prestava. A camada geográfica
/// ficava protegida pela conferência, e a lista por documento — que é a que
/// pega os quase cinquenta mil embargos sem área delimitada — era destruída do
/// mesmo jeito. A regra EMB-002 continuaria sendo dada como avaliada, e
/// passaria todo mundo.
/// </remarks>
public class ListaRestritiva
{
    public TipoRestricao Tipo { get; set; }

    public string Nome { get; set; } = default!;

    public string Origem { get; set; } = default!;

    /// <summary>Versão dos registros que a consulta enxerga.</summary>
    public int VersaoAtual { get; set; }

    public int TotalRegistros { get; set; }

    public DateTime? AtualizadaEm { get; set; }

    /// <summary>De quantos em quantos dias a fonte deve ser recarregada.</summary>
    public int? PeriodicidadeDias { get; set; }

    public DateTime? VenceEm =>
        PeriodicidadeDias.HasValue && AtualizadaEm.HasValue
            ? AtualizadaEm.Value.AddDays(PeriodicidadeDias.Value)
            : null;

    public bool Vencida => VenceEm.HasValue && VenceEm.Value < DateTime.UtcNow;
}
