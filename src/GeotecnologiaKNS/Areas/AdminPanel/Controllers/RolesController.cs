using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    public class RolesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RolesController(ApplicationDbContext context)
        {
            _context = context;
        }
        [Authorize(Policy = "UserCanTenantCreate")]
        public IActionResult Index()
        {
            return View(_context.Roles);
        }

        // GET: Roles/Create
        [Authorize(Policy = "UserCanTenantCreate")]
        public ActionResult Create()
        {
            if (User.Identity == null)
            {
                return Unauthorized();
            }

            var avaliableFeatures = User.Identity
                                        .GetFeatures()
                                        .Select(c => new IdentityRoleClaim<string> { ClaimType = c.Type, ClaimValue = c.Value })
                                        .ToList();

            return View(new ApplicationRole() { Claims = avaliableFeatures });
        }

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateAsync(ApplicationRole model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Custom = true;
            model.Id = model.Name;
            model.NormalizedName = model.Name.ToUpper();

            await _context.Roles.AddAsync(model);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // GET: Roles/Edit/5
        [Authorize(Policy = "UserCanTenantCreate")]
        public async Task<ActionResult> EditAsync(string id)
        {
            var role = await _context.Roles
                                     .Include(x => x.Claims)
                                     .FirstAsync(x => x.Id == id);

            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditAsync(ApplicationRole model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.RoleClaims.UpdateRange(model.Claims);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // GET: Roles/Delete/5
        [Authorize(Policy = "UserCanTenantCreate")]
        public ActionResult Delete(string id)
        {
            var model = _context.Roles.Find(id);

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmedAsync(string id)
        {
            var model = _context.Roles.Find(id);

            if (model != null)
            {
                _context.Roles.Remove(model);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
