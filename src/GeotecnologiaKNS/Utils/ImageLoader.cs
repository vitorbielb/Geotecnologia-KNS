namespace GeotecnologiaKNS.Utils;

/// <summary>
/// Publica a logo da indústria como arquivo estático e devolve o caminho.
/// </summary>
/// <remarks>
/// A logo é gravada no banco, mas servida do disco: assim a barra superior a
/// carrega como qualquer outra imagem, com cache do navegador, sem passar por
/// um endpoint a cada tela.
///
/// O arquivo é só um espelho do banco. A primeira versão saía cedo quando o
/// arquivo já existia, e isso congelava a logo: trocar a imagem no cadastro
/// nunca trocava a exibida, porque o espelho nunca era revisto.
/// </remarks>
public class ImageLoader
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageLoader> _logger;

    public ImageLoader(IWebHostEnvironment environment, ILogger<ImageLoader> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Carrega a logo da indústria e devolve o caminho relativo para exibi-la,
    /// ou <c>null</c> quando a indústria não tem logo.
    /// </summary>
    public string? LoadIndustryLogo(Industria industria)
    {
        ArgumentNullException.ThrowIfNull(industria);

        var pastaImagens = Path.Combine(_environment.WebRootPath, "images", "industrias");
        var caminho = Path.Combine(pastaImagens, $"logo_industria_tenant_{industria.TenantId}.png");

        try
        {
            if (industria.Imagem is null || industria.Imagem.Length == 0)
            {
                // Indústria sem logo: um arquivo remanescente continuaria sendo
                // servido, mostrando uma marca que já foi retirada do cadastro.
                if (File.Exists(caminho))
                {
                    File.Delete(caminho);
                }

                return null;
            }

            if (!EspelhoEstaEmDia(caminho, industria.Imagem))
            {
                Directory.CreateDirectory(pastaImagens);
                File.WriteAllBytes(caminho, industria.Imagem);
            }

            return Path.GetRelativePath(_environment.WebRootPath, caminho);
        }
        catch (IOException ex)
        {
            // Isto roda montando as claims do login. Disco cheio, permissão ou
            // dois acessos simultâneos da mesma indústria não podem impedir
            // alguém de entrar — a barra superior fica sem a marca, e pronto.
            _logger.LogWarning(ex,
                "Não foi possível publicar a logo da indústria {TenantId}.", industria.TenantId);

            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Sem permissão para publicar a logo da indústria {TenantId}.", industria.TenantId);

            return null;
        }
    }

    private static bool EspelhoEstaEmDia(string caminho, byte[] conteudo)
    {
        var info = new FileInfo(caminho);

        if (!info.Exists || info.Length != conteudo.Length)
        {
            return false;
        }

        return File.ReadAllBytes(caminho).AsSpan().SequenceEqual(conteudo);
    }
}
