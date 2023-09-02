using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Principal;

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
        var industria = await _context.Industrias.FindAsync(user.TenantId);

        if (industria == null)
        {
            throw IndustriaNotFoundForUser(user);
        }

        var logoPath = _imageLoader.LoadIndustryLogo(industria);

        ClaimsIdentity claimsIdentity = principal.Identity.ToClaimIdentity();

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
            if (claim == null)
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

    private static InvalidOperationException IndustriaNotFoundForUser(ApplicationUser user) => new InvalidOperationException(
            string.Format("Um erro ocorreu ao tentar executar a busca pela a industria de um funcionario. usuario: {0} industria: {1}",
            user.Email,
            user.TenantId));
}

public static class AppClaimsPrincipalExt
{
    public static ClaimsIdentity ToClaimIdentity(this IIdentity? identity)
    {
        return ((ClaimsIdentity)identity!);
    }

    public static string GetIndustria(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst("industria_nome");

        if (claim != null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool IsApplicationAdmin(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst(ClaimTypes.Role);
        return claim!.Value == nameof(Roles.ApplicationAdmin);
    }


    public static bool IsTenantAdmin(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst(ClaimTypes.Role);
        return claim!.Value == nameof(Roles.TenantAdmin);
    }


    public static int GetTenantId(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst("tenantId");
        if (claim == null) throw new InvalidOperationException("User tenant id should not be null.");
        return int.Parse(claim.Value);
    }

    public static string GetLogoPath(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst("industria_logo");

        if (claim != null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool HasEnabled<T>(this IIdentity identity, Expression<Func<T, object>>? operation = null)
        where T : IFeature, new()
    {
        if (operation == null)
        {
            return identity.ToClaimIdentity()
                           .Claims
                           .Where(c => c.Type.StartsWith(new T().FeatureName))
                           .All(c => c.Value == Enabled);
        }

        var operationName = OperationAcessor.GetName(operation);

        return identity.ToClaimIdentity()
                       .Claims
                       .Where(c => c.Type.StartsWith(new T().FeatureName) && c.Type.EndsWith(operationName))
                       .All(c => c.Value == Enabled);
    }

    public static IList<Claim> GetFeatures(this IIdentity identity)
    {
        var avaliableFeatures = Features.GetAll(defaultValue: Enabled);

        return identity.ToClaimIdentity()
                       .Claims
                       .Intersect(avaliableFeatures, new ClaimEqualityComparer())
                       .ToList();
    }
}

class ClaimEqualityComparer : IEqualityComparer<Claim>
{
    public bool Equals(Claim? x, Claim? y)
    {
        if (x == null || y == null) 
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

class OperationAcessor : ExpressionVisitor
{
    private string? _operationName;

    public static string GetName(Expression operation)
    {
        return new OperationAcessor(operation)._operationName!;
    }

    private OperationAcessor(Expression operation)
    {
        Visit(operation);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _operationName = node.Member.Name;
        return node;
    }
}