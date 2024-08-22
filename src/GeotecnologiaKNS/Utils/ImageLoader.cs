namespace GeotecnologiaKNS.Utils;

public class ImageLoader
{
    private readonly IWebHostEnvironment _environment;

    public ImageLoader(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Carrega logo da empresa e retorna caminho para acesso a logo no servidor.
    /// </summary>
    /// <param name="industria">Indústria contendo a logo.</param>
    /// <returns>Caminho relativo para a logo ou null caso não exista.</returns>
    public string? LoadIndustryLogo(Industria industria)
    {
        ArgumentNullException.ThrowIfNull(industria);

        var imageFolderPath = Path.Combine(_environment.WebRootPath, "images", "industrias");
        var fileName = $"logo_industria_tenant_{industria.TenantId}.png";
        var imagePath = Path.Combine(imageFolderPath, fileName);

        if (File.Exists(imagePath))
        {
            return GetRelativePath(imagePath);
        }

        if (industria.Imagem is null)
        {
            return null;
        }

        Directory.CreateDirectory(imageFolderPath);

        File.WriteAllBytes(imagePath, industria.Imagem);

        return GetRelativePath(imagePath);
    }

    private string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(_environment.WebRootPath, fullPath);
    }
}