using Microsoft.Extensions.Options;
using System.Linq.Expressions;
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

public static class AppClaimsPrincipalExt
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
        return claim!.Value == PermissionsByRole.ApplicationAdminRoleName;
    }


    public static bool IsTenantAdmin(this IIdentity identity)
    {
        var claim = ((ClaimsIdentity)identity).FindFirst(ClaimTypes.Role);
        return claim!.Value == PermissionsByRole.TenantAdminRoleName;
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

    public static bool HasEnabled<T>(this IIdentity identity, Expression<Func<T, object>>? operation = null)
        where T : IFeature, new()
    {
        if (operation == null)
        {
            return ((ClaimsIdentity)identity).Claims
                                             .Where(c => c.Type.StartsWith(new T().FeatureName))
                                             .All(c => c.Value == Global.Enabled);
        }

        var operationName = OperationAcessor.GetName(operation);

        return ((ClaimsIdentity)identity).Claims
                                         .Where(c => c.Type.StartsWith(new T().FeatureName) && c.Type.EndsWith(operationName))
                                         .All(c => c.Value == Global.Enabled);
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