using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    public class PermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public PermissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> IndexAsync()
        {
            if (User.Identity == null)
            {
                return Unauthorized();
            }

            int tenantId = User.Identity.TenantId();
            var industrias = await GetIndustriasAsync(tenantId);
            Permissions model = new(industrias);
            return View(model);
        }

        public async Task<IActionResult> GetUsers(int tenantId)
        {
            var users = await _context.Users
                                      .Where(user => user.TenantId == tenantId)
                                      .ToListAsync();

            var userSelectionItems = users.ToSelectListItems(
                text: x => x.UserName,
                value: x => x.Id);

            return Json(userSelectionItems);
        }

        public async Task<IActionResult> GetClaims(string userId)
        {
            var claims = await _context
                .UserClaims
                .Where(x => x.UserId == userId)
                .ToListAsync();

            return PartialView("_claimsEdit", claims);
        }

        private async Task<List<Industria>> GetIndustriasAsync(int tenantId)
        {
            if (User.Identity == null)
            {
                return GetIndustriasEmpty();
            }

            if (User.Identity.IsApplicationAdmin())
            {
                return await GetAllIndustrias();
            }

            if (User.Identity.IsTenantAdmin())
            {
                return await GetIndustriasByTenant(tenantId);
            }

            return new List<Industria>();

            #region private methods

            List<Industria> GetIndustriasEmpty()
            {
                return new List<Industria>();
            }

            async Task<List<Industria>> GetAllIndustrias()
            {
                return await _context
                    .Industrias
                    .ToListAsync();
            }

            async Task<List<Industria>> GetIndustriasByTenant(int tenantId)
            {
                return await _context
                    .Industrias
                    .Where(industria => industria.TenantId == tenantId)
                    .ToListAsync();
            }
            #endregion
        }
    }
}
