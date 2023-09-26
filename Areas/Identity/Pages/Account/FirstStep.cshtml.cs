using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeotecnologiaKNS.Areas.Identity.Pages.Account
{
    public class FirstStepModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public FirstStepModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int Id { get; set; }
        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel : Industria
        {
            public new IFormFile Imagem { get; set; }

        }

        public IActionResult OnGet()
        {
            if (_context.Industrias.Any())
            {
                return RedirectToPage("./SecondStep");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            using var memoryStream = new MemoryStream();
            await Input.Imagem.CopyToAsync(memoryStream);

            ((Industria)Input).Imagem = memoryStream.ToArray();

            _context.Industrias.Add(Input);
            await _context.SaveChangesAsync();

            return RedirectToPage("./SecondStep");
        }
    }
}

