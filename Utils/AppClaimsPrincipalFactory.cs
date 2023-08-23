using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Principal;

namespace GeotecnologiaKNS.Utils;

//https://www.linkedin.com/pulse/how-extend-aspnet-core-30-31-identity-user-omar-el-sergany/

public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly ApplicationDbContext _context;
    private readonly ImageLoader _imageLoader;

    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager
        , ApplicationDbContext context
        , RoleManager<IdentityRole> roleManager
        , IOptions<IdentityOptions> optionsAccessor
        , ImageLoader imageLoader)
        : base(userManager, roleManager, optionsAccessor)
    {
        _context = context;
        _imageLoader = imageLoader;
    }

    public async override Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        await base.CreateAsync(user);

        var principal = await base.CreateAsync(user);
        var industria = await _context.Industrias.FindAsync(user.TenantId);

        if (industria == null)
        {
            throw IndustriaNotFoundForUser(user);
        }

        var logoPath = _imageLoader.LoadIndustryLogo(industria);

        ((ClaimsIdentity)principal.Identity!).AddClaims(new[]
        {
            new Claim(type: "industria_nome", value: industria.Nome ),// Nome da industria
            new Claim(type: "tenantId", value: user.TenantId.ToString() ),// tenant atribuido ao usuario
        });

        if (logoPath != null)
        {
            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(type: "industria_logo", value: logoPath));// imagem da empresa
        }

        return principal;
    }

    private static InvalidOperationException IndustriaNotFoundForUser(ApplicationUser user) => new InvalidOperationException(
            string.Format("Um erro ocorreu ao tentar executar a busca pela a industria de um funcionario. usuario: {0} industria: {1}",
            user.Email,
            user.TenantId));
}

public static class AppClaimsPrincipalFactoryExtensions
{
    public static string Industria(this IIdentity identity)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst("industria_nome");

        if (claim != null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool IsApplicationAdmin(this IIdentity identity)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst(ClaimTypes.Role);
        return claim!.Value == Global.Roles.ApplicationAdmin;
    }


    public static bool IsTenantAdmin(this IIdentity identity)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst(ClaimTypes.Role);
        return claim!.Value == Global.Roles.TenantAdmin;
    }


    public static int TenantId(this IIdentity identity)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst("tenantId");
        if (claim == null) throw new InvalidOperationException("User tenant id should not be null.");
        return int.Parse(claim.Value);
    }

    public static string Logo(this IIdentity identity)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst("industria_logo");

        if (claim != null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool HasEnabled(this IIdentity identity, string feature)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst(feature);
        return claim?.Value == "enabled";
    }
}