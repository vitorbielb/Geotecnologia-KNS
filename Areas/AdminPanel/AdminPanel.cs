using Microsoft.AspNetCore.Mvc.Razor;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;

namespace GeotecnologiaKNS.Infra;

//http://blog.geveo.com/Claim-based-authorization-ASP-core

/// <summary>
/// Seeder class for initializing user roles and claims.
/// </summary>
public static class AdminRolesClaimsSeeder
{
    /// <summary>
    /// Seed user roles and claims asynchronously.
    /// </summary>
    /// <param name="app">The application builder.</param>
    public static async Task SeedRoleClaimsAsync(this IApplicationBuilder app)
    {
        using var di = app.ApplicationServices.CreateScope();
        var services = di.ServiceProvider;

        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        await CreateOrUpdateRolesClaims(roleManager, context);
        await RemoveUserRoles(context);
    }

    /// <summary>
    /// Creates or updates roles and their claims.
    /// </summary>
    /// <param name="roleManager">The RoleManager instance.</param>
    /// <param name="context">The application database context.</param>
    private static async Task CreateOrUpdateRolesClaims(RoleManager<ApplicationRole> roleManager, ApplicationDbContext context)
    {
        foreach (var claims in Roles.GetRoleClaims())
        {
            if (!await roleManager.RoleExistsAsync(claims.Role.Id))
            {
                await roleManager.CreateAsync(claims.Role);
            }

            var role = await roleManager.FindByIdAsync(claims.Role.Id);
            var persistedClaims = await context.RoleClaims.Where(c => c.RoleId == role.Id).ToListAsync();

            await UpdateAsync(context, claims, persistedClaims);
            await AddAsync(roleManager, claims, persistedClaims, role);
        }
    }

    /// <summary>
    /// Removes user roles not in application.
    /// </summary>
    /// <param name="context">The application database context.</param>
    private static async Task RemoveUserRoles(ApplicationDbContext context)
    {
        var applicationRoles = Roles
            .GetRoleClaims()
            .Select(c => c.Role.Name);

        var rangeToRemove = context
            .UserRoles
            .Where(ur => !context.Roles
                                 .Where(r => r.Id == ur.RoleId && r.Custom)
                                 .Any() && !applicationRoles.Contains(ur.RoleId));

        context.UserRoles.RemoveRange(rangeToRemove);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Adds claims to a role if they do not already exist.
    /// </summary>
    /// <param name="roleManager">The RoleManager instance.</param>
    /// <param name="claims">The claims to be added.</param>
    /// <param name="persistedClaims">The existing claims for the role.</param>
    /// <param name="role">The role to which claims are added.</param>
    private static async Task AddAsync(
          RoleManager<ApplicationRole> roleManager
        , RoleClaims claims
        , List<IdentityRoleClaim<string>> persistedClaims
        , ApplicationRole role)
    {
        foreach (var claim in claims)
        {
            if (persistedClaims.Any(c => c.ClaimType == claim.Type))
            {
                continue;
            }

            await roleManager.AddClaimAsync(role, claim);
        }
    }

    /// <summary>
    /// Updates existing claims for a role.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="claims">The new claims to be updated.</param>
    /// <param name="persistedClaims">The existing claims for the role.</param>
    private static async Task UpdateAsync(
          ApplicationDbContext context
        , RoleClaims claims
        , List<IdentityRoleClaim<string>> persistedClaims)
    {
        if (persistedClaims.Count == 0)
        {
            return;
        }

        foreach (var claim in claims)
        {
            var persistedClaim = persistedClaims.FirstOrDefault(c => c.ClaimType == claim.Type);

            if (persistedClaim == null)
            {
                continue;
            }

            if (persistedClaim.ClaimValue == claim.Value)
            {
                continue;
            }

            persistedClaim.ClaimValue = claim.Value;
        }

        context.RoleClaims.UpdateRange(persistedClaims);
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Visitor class for processing permission expressions.
/// </summary>
public class PermissionExpressionVisitor : ExpressionVisitor
{
    private readonly List<string> _featuresNames = new List<string>();

    public List<Claim> Claims { get; private set; } = new List<Claim>();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(Features.Everything))
        {
            SetAllPermissionsClaimsEnabled();
        }
        else if (node.Method.Name == nameof(Features.EverythingExcept))
        {
            Visit(node.Arguments[0]);
            SetEveryPermissionClaimsExcept(_featuresNames);
        }
        else if (node.Method.Name == nameof(Features.OnlyAccess))
        {
            Visit(node.Arguments[0]);
            SetAccessOnlyForPermissionClaimsIn(_featuresNames);
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        string originalString = node.ToString();
        int firstDotIndex = originalString.IndexOf('.');
        string result = originalString[(firstDotIndex + 1)..];

        if (!_featuresNames.Any(val => val.StartsWith(result)))
        {
            _featuresNames.Add(result);
        }

        return base.VisitMember(node);
    }

    private void SetAllPermissionsClaimsEnabled()
    {
        Claims.AddRange(Features.GetAll(defaultValue: Enabled));
    }

    private void SetEveryPermissionClaimsExcept(List<string> featuresNames)
    {
        Claims.AddRange(Features.GetAll(defaultValue: Disabled).Where(c => featuresNames.Any(f => c.Type.StartsWith(f))));
        Claims.AddRange(Features.GetAll(defaultValue: Enabled).Where(c => !featuresNames.Any(f => c.Type.StartsWith(f))));
    }

    private void SetAccessOnlyForPermissionClaimsIn(List<string> featuresNames)
    {
        Claims.AddRange(Features.GetAll(defaultValue: Disabled).Where(c => !featuresNames.Any(f => c.Type.StartsWith(f))));
        Claims.AddRange(Features.GetAll(defaultValue: Enabled).Where(c => featuresNames.Any(f => c.Type.StartsWith(f))));
    }
}

/// <summary>
/// Custom role claims class for defining claims associated with a role.
/// </summary>
[DebuggerDisplay("{RoleName} {Claims}")]
public class RoleClaims : IEnumerable<Claim>
{
    private ApplicationRole? _role;

    public RoleClaims(string name, Expression<Func<Features, object>> access)
    {
        RoleName = name;
        Claims = Build(access);
    }

    public string RoleName { get; set; } = default!;

    public List<Claim> Claims { get; set; } = new List<Claim>();

    public IEnumerator<Claim> GetEnumerator() => Claims.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ApplicationRole Role => _role ??= new()
    {
        Id = RoleName,
        Name = RoleName,
        NormalizedName = RoleName
    };

    private static List<Claim> Build(Expression<Func<Features, object>> featuresAccess)
    {
        var visitor = new PermissionExpressionVisitor();
        visitor.Visit(featuresAccess);
        return visitor.Claims;
    }
}

/// <summary>
/// Extension methods for configuring AdminPanel.
/// </summary>
public static class AdminPanelDiExtension
{
    /// <summary>
    /// Adds configuration for AdminPanel views.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAdminPanel(this IServiceCollection services)
    {
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationFormats.Add("/Areas/AdminPanel/Views/{1}/{0}.cshtml");
        });

        return services;
    }
}