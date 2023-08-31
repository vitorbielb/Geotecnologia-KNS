using System.Security.Claims;

namespace GeotecnologiaKNS.Infra
{
    //http://blog.geveo.com/Claim-based-authorization-ASP-core
    public static class AdminRolesClaimsSeeder
    {
        public static async Task SeedUserRolesClaimsAsync(this IApplicationBuilder app)
        {
            using var di = app.ApplicationServices.CreateScope();
            var services = di.ServiceProvider;

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            await CreateOrUpdateRolesClaims(roleManager);
            await SyncUserClaimsWithRoleClaimsAsync(userManager, roleManager);
        }

        private static async Task SyncUserClaimsWithRoleClaimsAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in await roleManager.Roles.ToListAsync())
            {
                var roleClaims = await roleManager.GetClaimsAsync(role);

                foreach (var user in await userManager.GetUsersInRoleAsync(role.Name))
                {
                    var userClaims = await userManager.GetClaimsAsync(user);

                    //Add em usuario quando existir em role e nao existir no usuario
                    await userManager.AddClaimsAsync(
                        user,
                        roleClaims.Where(roleClaim => !userClaims.Any(uc => uc.Type == roleClaim.Type)));

                    //Remova em usuario quando nao existir em role e existir no usuario
                    await userManager.RemoveClaimsAsync(
                        user,
                        userClaims.Where(userClaim => !roleClaims.Any(rc => rc.Type == userClaim.Type)));
                }
            }
        }

        private static async Task CreateOrUpdateRolesClaims(RoleManager<IdentityRole> roleManager)
        {
            foreach (var item in PermissionsByRole.Permissions)
            {
                if (!await roleManager.RoleExistsAsync(item.IdentityRole.Id))
                {
                    await roleManager.CreateAsync(item.IdentityRole);
                }

                var role = await roleManager.FindByIdAsync(item.IdentityRole.Id);
                var persistedClaims = await roleManager.GetClaimsAsync(role);

                await UpdateRemovedAsync(roleManager, item, role, persistedClaims);
                await UpdateAddedAsync(roleManager, item, role, persistedClaims);
            }
        }

        private static async Task UpdateAddedAsync(RoleManager<IdentityRole> roleManager, RoleClaims item, IdentityRole role, IList<Claim> persistedClaims)
        {
            foreach (var claim in item)
            {
                if (persistedClaims.Any(c => c.Type == claim.Type))
                {
                    continue;
                }

                await roleManager.AddClaimAsync(role, claim);
            }
        }

        private static async Task UpdateRemovedAsync(RoleManager<IdentityRole> roleManager, RoleClaims item, IdentityRole role, IList<Claim> persistedClaims)
        {
            foreach (var claim in persistedClaims)
            {
                if (item.Any(c => c.Type == claim.Type))
                {
                    continue;
                }

                await roleManager.RemoveClaimAsync(role, claim);
            }
        }
    }
}
