using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Security.Claims;

namespace GeotecnologiaKNS.Infra;

//https://www.linkedin.com/pulse/how-extend-aspnet-core-30-31-identity-user-omar-el-sergany/

public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ImageLoader _imageLoader;

    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager
        , ApplicationDbContext context
        , RoleManager<ApplicationRole> roleManager
        , IOptions<IdentityOptions> optionsAccessor
        , ImageLoader imageLoader)
        : base(userManager, roleManager, optionsAccessor)
    {
        _userManager = userManager;
        _context = context;
        _roleManager = roleManager;
        _imageLoader = imageLoader;
    }

    public async override Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var industria = await _context.Industrias.FindAsync(user.TenantId) ?? throw IndustriaNotFoundForUser(user);
        var logoPath = _imageLoader.LoadIndustryLogo(industria);
        var claimsIdentity = principal.Identity.ToClaimIdentity();

        claimsIdentity.AddClaims(new[]
        {
            new Claim(type: "industria_nome", value: industria.Nome ),// Nome da industria
            new Claim(type: "tenantId", value: user.TenantId.ToString() ),// tenant atribuido ao usuario
        });

        if (logoPath != null)
        {
            claimsIdentity.AddClaim(new Claim(type: "industria_logo", value: logoPath));// imagem da empresa
        }

        await AddRoleClaims(user, claimsIdentity);

        return principal;
    }

    private async Task AddRoleClaims(ApplicationUser user, ClaimsIdentity claimsIdentity)
    {
        await foreach (var claim in GetUserRoleClaims(user))
        {
            if (claim is null)
            {
                continue;
            }

            claimsIdentity.AddClaim(claim.ToClaim());
        }
    }

    private IAsyncEnumerable<IdentityRoleClaim<string>?> GetUserRoleClaims(ApplicationUser user)
    {
        return _context.RoleClaims
            .Where(c => _context.UserRoles
                                .Where(ur => ur.UserId == user.Id)
                                .Select(ur => ur.RoleId)
                                .Contains(c.RoleId))
            .GroupBy(c => c.ClaimType)
            .Select(group => group.OrderByDescending(c => c.ClaimValue == Enabled)
                                  .FirstOrDefault())
            .AsAsyncEnumerable();
    }

    private static InvalidOperationException IndustriaNotFoundForUser(ApplicationUser user)
    {
        const string Format = "Um erro ocorreu ao tentar executar a busca pela a industria de um funcionario. usuario: {0} industria: {1}";
        return new(string.Format(Format, user.Email, user.TenantId));
    }
}

class ClaimEqualityComparer : IEqualityComparer<Claim>
{
    public bool Equals(Claim? x, Claim? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        return x.Type == y.Type && x.Value == y.Value;
    }

    public int GetHashCode([DisallowNull] Claim obj)
    {
        int hash = 17;
        hash = hash * 23 + obj.Type.GetHashCode();
        hash = hash * 23 + obj.Value.GetHashCode();
        return hash;
    }
}
