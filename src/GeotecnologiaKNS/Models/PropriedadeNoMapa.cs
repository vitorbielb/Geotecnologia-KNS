namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Um imóvel como o mapa do painel precisa dele.
    /// </summary>
    /// <param name="Situacao">
    /// Veredito da análise mais recente, ou nulo quando o imóvel nunca foi
    /// analisado.
    /// </param>
    /// <remarks>
    /// Projeção própria em vez da entidade inteira porque o mapa carrega todos
    /// os imóveis da indústria de uma vez, e o perímetro de cada um já são
    /// alguns quilobytes. Trazer junto documentos e produtor multiplicaria isso
    /// sem nada aparecer na tela.
    ///
    /// Nulo e <see cref="Status.Solicitado"/> são coisas diferentes, e o mapa
    /// as pinta diferente: nunca analisado não é o mesmo que em análise, e
    /// nenhum dos dois é liberado.
    /// </remarks>
    public record PropriedadeNoMapa(
        string? Nome,
        string? Municipio,
        double Latitude,
        double Longitude,
        string? PerimetroGeoJson,
        Status? Situacao);
}
