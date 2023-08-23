using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.

namespace GeotecnologiaKNS.Utils
{
    public static class AdminRolesClaimsSeeder
    {
        private const string PermissionsEmbbededResource = "GeotecnologiaKNS.permissions.json";

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
            await foreach (var item in GetPermissionsAsync())
            {
                if (!await roleManager.RoleExistsAsync(item.Role.Id))
                {
                    await roleManager.CreateAsync(item.Role);
                }

                var role = await roleManager.FindByIdAsync(item.Role.Id);
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

        private static async IAsyncEnumerable<RoleClaims> GetPermissionsAsync()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(PermissionsEmbbededResource)!;
            using var reader = new StreamReader(stream);

            string fileContent = await reader.ReadToEndAsync();

            var items = JsonConvert.DeserializeObject<RoleClaims[]>(fileContent, new Converter())!;

            foreach (var item in items)
                yield return item;
        }
    }

    [DebuggerDisplay("{Name} {Claims}")]
    public class RoleClaims : IEnumerable<Claim>
    {
        private IdentityRole? _role;

        public string Name { get; set; } = default!;
        public List<Claim> Claims { get; set; } = new List<Claim>();

        public IEnumerator<Claim> GetEnumerator() => Claims.AsEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IdentityRole Role => _role ??= new()
        {
            Id = Name,
            Name = Name,
            NormalizedName = Name
        };
    }

    class Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RoleClaims[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            return JToken.Load(reader)
                         .Select(RoleClaims)
                         .ToArray();
        }

        private static RoleClaims RoleClaims(JToken token)
        {
            var roleClaims = new RoleClaims { Name = ((JProperty)token).Name };

            foreach (var item in token)
            {
                foreach (JProperty subItem in item)
                {
                    roleClaims.Claims.AddRange(subItem.First
                                                      .ToObject<string[]>()
                                                      .Select(str => string.Format("{0}_{1}", subItem.Name, str))
                                                      .Select(p => new Claim(p, "enabled")));
                }
            }

            return roleClaims;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.