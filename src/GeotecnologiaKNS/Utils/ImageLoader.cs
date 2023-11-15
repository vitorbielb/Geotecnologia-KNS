using System.Text;

namespace GeotecnologiaKNS.Utils;

public class ImageLoader
{
    private readonly IWebHostEnvironment _environment;

    public ImageLoader(
        IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Carrega logo da empresa e retorna caminho para acesso a logo no servidor.
    /// </summary>
    /// <param name="industria">industria contendo a logo.</param>
    /// <returns>O caminho relativo raiz para a logo.png ou null caso a empresa nao tenha cadastrado uma logo.</returns>
    public string? LoadIndustryLogo(Industria industria)
    {
        var fileName = new StringBuilder()
            .Append("logo_")
            .Append("industria_")
            .Append("tenant_")
            .Append(industria.TenantId)
            .Append(".png")
            .ToString();

        string imagePath = Path.Combine(
            _environment.WebRootPath
            , "images"
            , "industrias"
            , fileName);

        if (File.Exists(imagePath))
        {
            return Path.GetRelativePath(_environment.WebRootPath, imagePath);
        }

        var logo = industria.Imagem;

        if (logo == null)
        {
            return null;
        }

        File.WriteAllBytes(imagePath, logo);

        return Path.GetRelativePath(_environment.WebRootPath, imagePath);
    }
}
