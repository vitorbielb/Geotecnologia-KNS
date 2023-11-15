using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Principal;

namespace GeotecnologiaKNS.Infra;

public static class AppClaimsPrincipalExt
{
    public static ClaimsIdentity ToClaimIdentity(this IIdentity? identity)
    {
        return (ClaimsIdentity)identity!;
    }

    public static string GetIndustria(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst("industria_nome");

        if (claim is not null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool IsApplicationAdmin(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst(ClaimTypes.Role);
        return claim?.Value == nameof(Roles.ApplicationAdmin);
    }


    public static bool IsTenantAdmin(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst(ClaimTypes.Role);
        return claim?.Value == nameof(Roles.TenantAdmin);
    }


    public static int? GetTenantId(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst("tenantId");
        if (claim is null) return null;
        return int.Parse(claim.Value);
    }

    public static string GetLogoPath(this IIdentity identity)
    {
        var claim = identity.ToClaimIdentity().FindFirst("industria_logo");

        if (claim is not null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool HasEnabled<T>(this IIdentity identity, Expression<Func<T, object>>? operation = null)
        where T : IFeature, new()
    {
        if (operation is null)
        {
            return identity.ToClaimIdentity()
                           .Claims
                           .Where(c => c.Type.StartsWith(typeof(T).Name))
                           .All(c => c.Value == Enabled);
        }

        var operationName = OperationAcessor.GetName(operation);

        return identity.ToClaimIdentity()
                       .Claims
                       .Where(c => c.Type.StartsWith(typeof(T).Name) && c.Type.EndsWith(operationName))
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
