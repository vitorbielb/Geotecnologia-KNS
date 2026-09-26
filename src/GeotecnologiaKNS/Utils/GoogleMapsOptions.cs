namespace GeotecnologiaKNS.Utils;

/// <summary>
/// Configuração de acesso à API do Google Maps.
/// </summary>
/// <remarks>
/// Em produção a chave deve vir de variável de ambiente
/// (<c>GoogleMaps__ApiKey</c>) ou de user-secrets, nunca do appsettings versionado.
/// </remarks>
public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Centro padrão do mapa quando não há coordenadas (centro geográfico do Brasil).</summary>
    public double DefaultLat { get; set; } = -14.235;

    public double DefaultLng { get; set; } = -51.925;
}
