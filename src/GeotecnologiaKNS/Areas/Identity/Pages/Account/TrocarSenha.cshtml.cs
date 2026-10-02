using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeotecnologiaKNS.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Troca obrigatória da senha provisória, no primeiro acesso.
    /// </summary>
    /// <remarks>
    /// Separada da ChangePassword do Identity de propósito: aquela pede a senha
    /// atual e pode ser abandonada a qualquer momento. Esta é o único destino
    /// alcançável enquanto a senha provisória valer, e por isso não pede a
    /// anterior — quem chegou aqui já provou conhecê-la ao entrar.
    /// </remarks>
    public class TrocarSenhaModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<TrocarSenhaModel> _logger;

        public TrocarSenhaModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<TrocarSenhaModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Campo obrigatório")]
            [StringLength(100, MinimumLength = 8,
                ErrorMessage = "A senha deve ter no mínimo 8 e no máximo 100 caracteres")]
            [DataType(DataType.Password)]
            [Display(Name = "Nova senha")]
            public string NovaSenha { get; set; } = string.Empty;

            [Required(ErrorMessage = "Campo obrigatório")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirme a nova senha")]
            [Compare(nameof(NovaSenha), ErrorMessage = "A senha e a confirmação não coincidem")]
            public string Confirmacao { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario is null)
            {
                return RedirectToPage("./Login");
            }

            // Quem já trocou não tem o que fazer aqui; o middleware não barra
            // esta página, então a checagem precisa ser dela mesma.
            return usuario.SenhaProvisoria ? Page() : LocalRedirect("~/");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario is null)
            {
                return RedirectToPage("./Login");
            }

            if (!usuario.SenhaProvisoria)
            {
                return LocalRedirect("~/");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Remover e definir, em vez de ChangePasswordAsync, porque a senha
            // atual não é pedida nesta tela.
            var remocao = await _userManager.RemovePasswordAsync(usuario);

            if (!remocao.Succeeded)
            {
                AdicionarErros(remocao);
                return Page();
            }

            var definicao = await _userManager.AddPasswordAsync(usuario, Input.NovaSenha);

            if (!definicao.Succeeded)
            {
                AdicionarErros(definicao);
                return Page();
            }

            usuario.SenhaProvisoria = false;
            await _userManager.UpdateAsync(usuario);

            // Regenera as claims da sessão: sem isso a marca de senha provisória
            // continuaria no cookie e o middleware devolveria o usuário para cá
            // em looping, mesmo com a senha já trocada.
            await _signInManager.RefreshSignInAsync(usuario);

            _logger.LogInformation("Usuário {UserId} trocou a senha provisória.", usuario.Id);

            return LocalRedirect("~/");
        }

        private void AdicionarErros(IdentityResult resultado)
        {
            foreach (var erro in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, erro.Description);
            }
        }
    }
}
