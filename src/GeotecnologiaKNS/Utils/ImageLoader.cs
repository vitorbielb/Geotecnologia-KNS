using System.IO;
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
        var imageFolderPath = Path.Combine(_environment.WebRootPath, "images", "industrias");
        var fileName = string.Format("logo_industria_tenant_{0}.png", industria.TenantId);
        var imagePath = Path.Combine(imageFolderPath, fileName);

        if (File.Exists(imagePath))
        {
            return Path.GetRelativePath(_environment.WebRootPath, imagePath);
        }

        if (!Directory.Exists(imageFolderPath))
        {
            Directory.CreateDirectory(imageFolderPath);
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
