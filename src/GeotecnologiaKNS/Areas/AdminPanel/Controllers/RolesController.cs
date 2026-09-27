using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    /// <summary>
    /// Administração de funções e permissões.
    /// </summary>
    /// <remarks>
    /// A policy está na classe, não nas actions: antes só as telas de GET
    /// exigiam permissão, e as três de POST ficavam abertas a qualquer usuário
    /// autenticado. Somado ao Edit, que gravava as claims recebidas do
    /// formulário sem verificar a quem pertenciam, qualquer pessoa podia
    /// conceder a si mesma qualquer permissão — inclusive sobre outra indústria.
    /// </remarks>
    [Authorize(Policy = "UserCanTenantCreate")]
    public class RolesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserContext _userContext;

        public RolesController(ApplicationDbContext context, IUserContext userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Roles.OrderBy(x => x.TenantId).ThenBy(x => x.Name).ToListAsync());
        }

        // GET: Roles/Create
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

            var tenantId = _userContext.TenantId;

            if (!tenantId.HasValue || tenantId <= 0)
            {
                return Forbid();
            }

            var nome = (model.Name ?? string.Empty).Trim();
            var normalizado = nome.ToUpperInvariant();

            if (nome.Length == 0)
            {
                ModelState.AddModelError(nameof(ApplicationRole.Name), "Informe o nome da função.");
                return View(model);
            }

            // Colidir com papel interno traria ambiguidade real: a busca por nome
            // passaria a encontrar dois registros, o global e o do inquilino.
            if (await _context.Roles.AnyAsync(x => x.TenantId == RoleGlobal.TenantId && x.NormalizedName == normalizado))
            {
                ModelState.AddModelError(nameof(ApplicationRole.Name),
                    "Já existe uma função interna com este nome. Escolha outro.");
                return View(model);
            }

            if (await _context.Roles.AnyAsync(x => x.TenantId == tenantId && x.NormalizedName == normalizado))
            {
                ModelState.AddModelError(nameof(ApplicationRole.Name), "Sua indústria já tem uma função com este nome.");
                return View(model);
            }

            var claims = model.Claims ?? new List<IdentityRoleClaim<string>>();

            // Id gerado, não o nome: o Id é chave primária global, e usar o nome
            // impediria duas indústrias de terem funções homônimas.
            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = nome,
                NormalizedName = normalizado,
                TenantId = tenantId.Value,
                Custom = true
            };

            _context.Roles.Add(role);

            // Só as permissões que o próprio usuário possui podem ser concedidas:
            // ninguém cria uma função mais poderosa do que a sua.
            foreach (var claim in FiltrarClaimsPermitidas(claims))
            {
                _context.RoleClaims.Add(new IdentityRoleClaim<string>
                {
                    RoleId = role.Id,
                    ClaimType = claim.ClaimType,
                    ClaimValue = claim.ClaimValue
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // GET: Roles/Edit/5
        public async Task<ActionResult> EditAsync(string id)
        {
            var role = await BuscarRoleEditavelAsync(id);

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

            // Carrega pelo Id e confere a posse antes de gravar. A versão
            // anterior fazia UpdateRange nas claims vindas do formulário, o que
            // permitia alterar as permissões de qualquer função do sistema
            // apenas forjando o RoleId no POST.
            var role = await BuscarRoleEditavelAsync(model.Id);

            if (role == null)
            {
                return NotFound();
            }

            var permitidas = FiltrarClaimsPermitidas(model.Claims ?? new List<IdentityRoleClaim<string>>())
                .ToDictionary(c => c.ClaimType!, c => c.ClaimValue);

            foreach (var claim in role.Claims)
            {
                if (claim.ClaimType is not null && permitidas.TryGetValue(claim.ClaimType, out var valor))
                {
                    claim.ClaimValue = valor;
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // GET: Roles/Delete/5
        public async Task<ActionResult> Delete(string id)
        {
            var role = await BuscarRoleEditavelAsync(id);

            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmedAsync(string id)
        {
            var role = await BuscarRoleEditavelAsync(id);

            if (role == null)
            {
                return NotFound();
            }

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Busca uma função que o usuário atual tem direito de alterar.
        /// </summary>
        /// <remarks>
        /// Devolve null para função de outro inquilino — o filtro global já as
        /// esconde — e também para os papéis internos, que são compartilhados
        /// por todas as indústrias e não podem ser editados por um cliente.
        /// </remarks>
        private async Task<ApplicationRole?> BuscarRoleEditavelAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var role = await _context.Roles
                                     .Include(x => x.Claims)
                                     .FirstOrDefaultAsync(x => x.Id == id);

            if (role is null)
            {
                return null;
            }

            if (RoleGlobal.EhGlobal(role) && !_userContext.IsApplicationAdmin)
            {
                return null;
            }

            return role;
        }

        /// <summary>
        /// Mantém apenas as permissões que o próprio usuário tem habilitadas.
        /// Impede que alguém crie ou edite uma função mais poderosa que a sua.
        /// </summary>
        private IEnumerable<IdentityRoleClaim<string>> FiltrarClaimsPermitidas(
            IEnumerable<IdentityRoleClaim<string>> claims)
        {
            var disponiveis = User.Identity is null
                ? new HashSet<string>()
                : User.Identity.GetFeatures().Select(c => c.Type).ToHashSet();

            return claims.Where(c => c.ClaimType is not null && disponiveis.Contains(c.ClaimType));
        }
    }
}
