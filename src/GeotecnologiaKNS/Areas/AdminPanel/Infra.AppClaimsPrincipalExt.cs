using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;

namespace GeotecnologiaKNS.Infra;

public static class AppClaimsPrincipalExt
{
    public static ClaimsIdentity AsClaimIdentity(this IIdentity? identity)
    {
        return (ClaimsIdentity)identity!;
    }

    public static string GetIndustria(this IIdentity identity)
    {
        var claim = identity.AsClaimIdentity().FindFirst("industria_nome");

        if (claim is not null)
        {
            return claim.Value;
        }

        return string.Empty;
    }

    public static bool IsApplicationAdmin(this IIdentity identity)
    {
        var claim = identity.AsClaimIdentity().FindFirst(ClaimTypes.Role);
        return claim?.Value == nameof(Roles.ApplicationAdmin);
    }


    public static bool IsTenantAdmin(this IIdentity identity)
    {
        var claim = identity.AsClaimIdentity().FindFirst(ClaimTypes.Role);
        return claim?.Value == nameof(Roles.TenantAdmin);
    }


    public static int? GetTenantId(this IIdentity identity)
    {
        var claim = identity.AsClaimIdentity().FindFirst("tenantId");
        if (claim is null) return null;
        return int.Parse(claim.Value);
    }

    public static string GetLogoPath(this IIdentity identity, string requestPath)
    {
        var claim = identity.AsClaimIdentity().FindFirst("industria_logo");

        if (claim is null)
        {
            return string.Empty;
        }

        var backslashStringBuilder = new StringBuilder();

        for (int i = 0; i < requestPath.Count(c => c == '/'); i++)
        {
            backslashStringBuilder.Append("..\\");
        }

        var backslashString = backslashStringBuilder.ToString();

        return Path.Combine(backslashString, claim.Value);
    }

    public static bool HasEnabled(this IIdentity identity, Expression<Func<Features.List, IFeature>>? operation)
    {
        var visitor = new FeaturesNamesExpressionVisitor();
        var operationName = visitor.Visit(operation);
        var operations = visitor.OperationNames;

        return identity.AsClaimIdentity()
                       .Claims
                       .Where(c => operations.Contains(c.Type))
                       .All(c => c.Value == Enabled);
    }

    public static bool HasEnabled(this IIdentity identity, Expression<Func<Features.List, IOperation>>? operation)
    {
        var visitor = new FeaturesNamesExpressionVisitor();
        var operationName = visitor.Visit(operation);
        var operations = visitor.OperationNames;

        return identity.AsClaimIdentity()
                       .Claims
                       .Where(c => operations.Contains(c.Type))
                       .All(c => c.Value == Enabled);
    }

    public static IList<Claim> GetFeatures(this IIdentity identity)
    {
        var avaliableFeatures = Features.GetAll(defaultValue: Enabled);

        return identity.AsClaimIdentity()
                       .Claims
                       .Intersect(avaliableFeatures, new ClaimEqualityComparer())
                       .ToList();
    }
}
