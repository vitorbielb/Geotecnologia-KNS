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
