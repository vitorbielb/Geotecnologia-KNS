using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers
{
    [Authorize(Roles = nameof(Roles.Administrador))]
    public class IndustriasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<IndustriasController> _logger;

        public IndustriasController(
            ApplicationDbContext context,
            IPasswordHasher<ApplicationUser> passwordHasher,
            UserManager<ApplicationUser> userManager,
            ILogger<IndustriasController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Cadastro de uma indústria junto com o usuário que vai administrá-la.
        /// </summary>
        [Authorize(Policy = "UserCanTenantCreate")]
        public IActionResult Nova() => View(new NovaIndustriaViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "UserCanTenantCreate")]
        public async Task<IActionResult> Nova(NovaIndustriaViewModel viewModel, CancellationToken cancellationToken)
        {
            var cnpj = SomenteDigitos(viewModel.Cnpj);
            var email = viewModel.AdminEmail.Trim();
            var emailNormalizado = email.ToUpperInvariant();

            if (await _context.Industrias.AnyAsync(x => x.Cnpj == viewModel.Cnpj.Trim() || x.Cnpj == cnpj, cancellationToken))
            {
                ModelState.AddModelError(nameof(viewModel.Cnpj), "Já existe uma indústria com este CNPJ.");
            }

            // Sem filtro de inquilino nesta consulta porque o administrador da
            // aplicação enxerga todos: e-mail repetido em outra indústria
            // impediria o login, já que o Identity o usa como identificador.
            if (await _context.Users.AnyAsync(x => x.NormalizedEmail == emailNormalizado, cancellationToken))
            {
                ModelState.AddModelError(nameof(viewModel.AdminEmail), "Já existe um usuário com este e-mail.");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var industria = viewModel.ParaIndustria();

            // Grava só os dígitos, como as indústrias já cadastradas. Guardar ora
            // "11222333000181", ora "11.222.333/0001-81" faria a mesma empresa
            // entrar duas vezes sem que a checagem de duplicidade percebesse.
            industria.Cnpj = cnpj;

            if (viewModel.Logotipo is { Length: > 0 })
            {
                using var memoria = new MemoryStream();
                await viewModel.Logotipo.CopyToAsync(memoria, cancellationToken);
                industria.Imagem = memoria.ToArray();
            }

            // Transação explícita: a indústria precisa ser gravada antes para
            // receber o TenantId, mas uma indústria sem administrador fica órfã
            // — ninguém consegue entrar nela. Ou nascem os dois, ou nenhum.
            await using var transacao = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                _context.Industrias.Add(industria);
                await _context.SaveChangesAsync(cancellationToken);

                var usuario = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = viewModel.AdminUserName.Trim(),
                    NormalizedUserName = viewModel.AdminUserName.Trim().ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = emailNormalizado,
                    PhoneNumber = viewModel.AdminPhoneNumber?.Trim(),
                    EmailConfirmed = true,
                    TenantId = industria.TenantId,
                    SecurityStamp = Guid.NewGuid().ToString(),

                    // A senha é combinada por fora e chega conhecida por duas
                    // pessoas; só deixa de ser provisória quando o titular a troca.
                    SenhaProvisoria = true
                };

                usuario.PasswordHash = _passwordHasher.HashPassword(usuario, viewModel.AdminPassword);

                _context.Users.Add(usuario);
                await _context.SaveChangesAsync(cancellationToken);

                var vinculo = await _userManager.AddToRoleAsync(usuario, nameof(Roles.ClienteAdmin));

                if (!vinculo.Succeeded)
                {
                    // Sem o papel, o usuário entra mas não enxerga nada. Desfazer
                    // tudo é melhor do que entregar ao cliente um acesso inútil.
                    throw new InvalidOperationException(
                        "Falha ao atribuir o papel ClienteAdmin: " +
                        string.Join("; ", vinculo.Errors.Select(e => e.Description)));
                }

                await transacao.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Indústria {TenantId} ({Nome}) criada com o administrador {Email}.",
                    industria.TenantId, industria.Nome, email);

                TempData["Sucesso"] =
                    $"Indústria \"{industria.Nome}\" criada. O administrador {email} já pode entrar no sistema.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transacao.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Falha ao criar a indústria {Nome}; nada foi gravado.", viewModel.Nome);

                ModelState.AddModelError(string.Empty,
                    "Não foi possível concluir o cadastro. Nenhum dado foi gravado. Tente novamente.");

                return View(viewModel);
            }
        }

        private static string SomenteDigitos(string? valor) =>
            new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

        // GET: Industrias
        [Authorize(Policy = "UserCanTenantCreate")]
        public async Task<IActionResult> Index()
        {
              return _context.Industrias != null ? 
                          View(await _context.Industrias.ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.Industrias'  is null.");
        }

        // GET: Industrias/Details/5
        [Authorize(Policy = "UserCanTenantCreate")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Industrias == null)
            {
                return NotFound();
            }

            var industria = await _context.Industrias
                .FirstOrDefaultAsync(m => m.TenantId == id);
            if (industria == null)
            {
                return NotFound();
            }

            return View(industria);
        }

        // GET: Industrias/Edit/5
        [Authorize(Policy = "UserCanTenantCreate")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Industrias == null)
            {
                return NotFound();
            }

            var industria = await _context.Industrias.FindAsync(id);
            if (industria == null)
            {
                return NotFound();
            }
            return View(industria);
        }

        // POST: Industrias/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TenantId,Imagem,Nome,NomeResumido,RazaoSocial,Cnpj")] Industria industria)
        {
            if (id != industria.TenantId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(industria);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!IndustriaExists(industria.TenantId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(industria);
        }

        // GET: Industrias/Delete/5
        [Authorize(Policy = "UserCanTenantCreate")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Industrias == null)
            {
                return NotFound();
            }

            var industria = await _context.Industrias
                .FirstOrDefaultAsync(m => m.TenantId == id);
            if (industria == null)
            {
                return NotFound();
            }

            return View(industria);
        }

        // POST: Industrias/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Industrias == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Industrias'  is null.");
            }
            var industria = await _context.Industrias.FindAsync(id);
            if (industria != null)
            {
                _context.Industrias.Remove(industria);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool IndustriaExists(int id)
        {
          return (_context.Industrias?.Any(e => e.TenantId == id)).GetValueOrDefault();
        }
    }
}
