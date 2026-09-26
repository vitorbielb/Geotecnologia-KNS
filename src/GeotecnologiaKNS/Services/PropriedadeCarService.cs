using GeotecnologiaKNS.Geo;

namespace GeotecnologiaKNS.Services;

public enum ResultadoConsultaCar
{
    Encontrado = 0,
    CodigoInvalido = 1,
    NaoEncontrado = 2,
    BaseIndisponivel = 3
}

public record ConsultaCar(
    ResultadoConsultaCar Resultado,
    ImovelCarDto? Imovel,
    string Mensagem)
{
    public bool Sucesso => Resultado == ResultadoConsultaCar.Encontrado && Imovel is not null;
}

public interface IPropriedadeCarService
{
    Task<ConsultaCar> ConsultarAsync(string? codigoCar, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copia para a propriedade os dados resolvidos na base do CAR.
    /// O perímetro é copiado, e não referenciado, para que a propriedade continue
    /// auditável mesmo depois que a base for recarregada com outra versão.
    /// </summary>
    void Aplicar(Propriedade propriedade, ImovelCarDto imovel);
}

public class PropriedadeCarService : IPropriedadeCarService
{
    private readonly ICarLookupService _carLookup;

    public PropriedadeCarService(ICarLookupService carLookup)
    {
        _carLookup = carLookup;
    }

    public async Task<ConsultaCar> ConsultarAsync(string? codigoCar, CancellationToken cancellationToken = default)
    {
        var normalizado = CodigoCar.Normalizar(codigoCar);

        if (normalizado is null)
        {
            return new ConsultaCar(
                ResultadoConsultaCar.CodigoInvalido,
                null,
                "Número do CAR inválido. O formato esperado é UF-CódigoIBGE-Hash, por exemplo MT-5107925-A1B2...");
        }

        if (!await _carLookup.BaseDisponivelAsync(cancellationToken))
        {
            return new ConsultaCar(
                ResultadoConsultaCar.BaseIndisponivel,
                null,
                "A base do CAR ainda não foi carregada. Procure o administrador do sistema.");
        }

        var imovel = await _carLookup.ObterPorCodigoAsync(normalizado, cancellationToken);

        if (imovel is null)
        {
            return new ConsultaCar(
                ResultadoConsultaCar.NaoEncontrado,
                null,
                "CAR não encontrado na versão carregada da base. Isso pode significar cadastro recente ainda não publicado, e não necessariamente CAR inválido.");
        }

        return new ConsultaCar(ResultadoConsultaCar.Encontrado, imovel, "Imóvel localizado na base do CAR.");
    }

    public void Aplicar(Propriedade propriedade, ImovelCarDto imovel)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        ArgumentNullException.ThrowIfNull(imovel);

        propriedade.CodigoCar = imovel.CodigoCar;
        propriedade.PerimetroGeoJson = imovel.PerimetroGeoJson;
        propriedade.PerimetroOrigem = imovel.Origem;
        propriedade.PerimetroAtualizadoEm = imovel.BaseCarregadaEm ?? DateTime.Now;
        propriedade.SituacaoCar = imovel.Situacao;

        propriedade.Latitude = imovel.CentroLat;
        propriedade.Longitude = imovel.CentroLng;
        propriedade.OrigemCoordenadas = $"CAR ({imovel.Origem})";

        // Prefere a área calculada da geometria: a área declarada no CAR é o que
        // o produtor informou, e a divergência entre as duas é sinal de alerta.
        var area = imovel.AreaCalculadaHa ?? imovel.AreaHa;

        if (area.HasValue)
        {
            propriedade.Area = area.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(imovel.Municipio))
        {
            propriedade.Municipio = imovel.Municipio;
        }

        if (Enum.TryParse<Estados>(imovel.Uf, ignoreCase: true, out var uf))
        {
            propriedade.UnidadeFederativa = uf;
        }

        if (string.IsNullOrWhiteSpace(propriedade.NomePropriedade))
        {
            propriedade.NomePropriedade = MontarNomePadrao(imovel);
        }

        // O perímetro veio da base oficial: não há o que um humano validar no
        // cadastro em si. A análise de conformidade é outra etapa.
        propriedade.Validacao = Validacao.Validado;
    }

    private static string MontarNomePadrao(ImovelCarDto imovel)
    {
        if (!string.IsNullOrWhiteSpace(imovel.Municipio))
        {
            return $"{imovel.Municipio}/{imovel.Uf} - {imovel.CodigoCar[^6..]}";
        }

        return imovel.CodigoCar;
    }
}
