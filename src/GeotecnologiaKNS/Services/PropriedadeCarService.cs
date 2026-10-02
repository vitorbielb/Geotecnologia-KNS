using GeotecnologiaKNS.Geo;

namespace GeotecnologiaKNS.Services;

public enum ResultadoConsultaCar
{
    Encontrado = 0,
    CodigoInvalido = 1,
    NaoEncontrado = 2,

    /// <summary>Há conexão com o PostGIS, mas nenhuma carga da base foi concluída.</summary>
    BaseIndisponivel = 3,

    /// <summary>O acesso ao PostGIS não está configurado neste servidor.</summary>
    NaoConfigurado = 4,

    /// <summary>
    /// O município do código ainda não teve a base carregada. Distinto de
    /// <see cref="NaoEncontrado"/>: aqui a pendência é de quem opera o serviço,
    /// não do dado que o cliente informou.
    /// </summary>
    MunicipioNaoCoberto = 5
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
    private readonly IUserContext _userContext;
    private readonly ILogger<PropriedadeCarService> _logger;

    public PropriedadeCarService(
        ICarLookupService carLookup,
        IUserContext userContext,
        ILogger<PropriedadeCarService> logger)
    {
        _carLookup = carLookup;
        _userContext = userContext;
        _logger = logger;
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

        // Os dois casos abaixo davam a mesma mensagem, e têm causas diferentes:
        // num falta configurar o banco geoespacial, no outro falta carregar a
        // base nele. Quem vai resolver precisa saber qual dos dois é.
        if (!_carLookup.EstaConfigurado)
        {
            return new ConsultaCar(
                ResultadoConsultaCar.NaoConfigurado,
                null,
                "O banco geoespacial não está configurado neste servidor " +
                "(connection string 'Geo'). Sem ele não é possível consultar o CAR.");
        }

        if (!await _carLookup.BaseDisponivelAsync(cancellationToken))
        {
            return new ConsultaCar(
                ResultadoConsultaCar.BaseIndisponivel,
                null,
                "O banco geoespacial está acessível, mas a base do CAR ainda não foi importada. " +
                "É preciso executar a carga antes de cadastrar imóveis.");
        }

        var imovel = await _carLookup.ObterPorCodigoAsync(normalizado, cancellationToken);

        if (imovel is not null)
        {
            return new ConsultaCar(ResultadoConsultaCar.Encontrado, imovel, "Imóvel localizado na base do CAR.");
        }

        // O código do CAR carrega o município dentro dele. Se esse município
        // nunca foi carregado, a ausência do imóvel não diz nada sobre o CAR —
        // e a pendência é de quem opera o serviço, não do cliente.
        var codigoIbge = CodigoCar.ExtrairCodigoIbge(normalizado);
        var uf = CodigoCar.ExtrairUf(normalizado) ?? string.Empty;

        if (codigoIbge is not null && !await _carLookup.MunicipioCobertoAsync(codigoIbge, cancellationToken))
        {
            await RegistrarLacunaSemInterromperAsync(codigoIbge, uf, normalizado, cancellationToken);

            return new ConsultaCar(
                ResultadoConsultaCar.MunicipioNaoCoberto,
                null,
                $"A base deste município ({uf}, código IBGE {codigoIbge}) ainda não foi carregada no sistema. " +
                "O pedido foi registrado e o município entrará na próxima carga.");
        }

        return new ConsultaCar(
            ResultadoConsultaCar.NaoEncontrado,
            null,
            "O município está na base, mas este CAR não foi localizado nele. " +
            "Confira o número; pode também ser cadastro recente, ainda não publicado pelo SICAR.");
    }

    /// <summary>
    /// Falhar ao anotar a lacuna não pode derrubar a consulta: o registro serve
    /// para priorizar carga, não para responder ao usuário.
    /// </summary>
    private async Task RegistrarLacunaSemInterromperAsync(
        string codigoIbge, string uf, string codigoCar, CancellationToken cancellationToken)
    {
        try
        {
            await _carLookup.RegistrarLacunaAsync(
                codigoIbge, uf, codigoCar, _userContext.TenantId ?? 0, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível registrar a lacuna de cobertura de {CodigoIbge}.", codigoIbge);
        }
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
