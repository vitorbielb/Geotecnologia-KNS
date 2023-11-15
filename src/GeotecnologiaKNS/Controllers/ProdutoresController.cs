using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GeotecnologiaKNS.Controllers;

[Authorize]
public class ProdutoresController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProdutoresController(ApplicationDbContext dbcontext)
    {
        _context = dbcontext;
    }

    // GET: Produtores
    public async Task<ActionResult> IndexAsync()
    {
        var produtores = await _context.Produtores.ToListAsync();
        return View(produtores);
    }
    public async Task<ActionResult> AnaliseAsync()
    {
        var produtores = await _context.Produtores.ToListAsync();
        return View(produtores);
    }
    private ActionResult HttpNotFound()
    {
        return NotFound();
    }

    // GET: Produtores/Create
    public ActionResult Create()
    {
        return View();
    }

    // POST: Produtores/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [TenantFilter]
    public async Task<ActionResult> CreateAsync(Produtor produtor)
    {
        ModelState.Remove("Documentos");

        if (ModelState.IsValid)
        {
            await _context.Produtores.AddAsync(produtor);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        return View(produtor);
    }
    public async Task<ActionResult> EditAsync(int id)
    {
        ViewBag.Situacao = ((Situacao[])Enum.GetValues(typeof(Situacao)))
               .ToSelectListItems(
                   x => x.ToString(),
                   x => (int)x,
                   options => options.Placeholder = "Selecione...");
        var produtor = await _context.Produtores
                                     .Include(x => x.Propriedades)
                                     .Include(x => x.Documentos)
                                     .FirstAsync(x => x.Id == id);

        if (produtor == null)
        {
            return HttpNotFound();
        }

        return View(produtor);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [TenantFilter]
    public async Task<ActionResult> EditAsync(Produtor produtor)
    {
        ModelState.Remove("Documentos");

        if (ModelState.IsValid)
        {
            _context.Produtores.Update(produtor);
            await _context.SaveChangesAsync();
            return RedirectToAction("Analise", "Produtores");
        }

        var persitedProdutor = await _context.Produtores
                                     .Include(x => x.Propriedades)
                                     .Include(x => x.Documentos)
                                     .FirstAsync(x => x.Id == produtor.Id);

        produtor.Propriedades = persitedProdutor.Propriedades;
        produtor.Documentos = persitedProdutor.Documentos;

        return View(produtor);
    }
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null || _context.Produtores == null)
        {
            return NotFound();
        }

        var produtores = await _context.Produtores
            .Include(s => s.Propriedades)
            .Include(p => p.Documentos)
            .Include(y => y.Solicitacoes)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (produtores == null)
        {
            return NotFound();
        }

        return View(produtores);
    }

    // GET: Produtores/Edit/5


    // GET: Produtores/Delete/5
    public ActionResult Delete(int id)
    {
        var produtor = _context.Produtores.Find(id);

        if (produtor == null)
        {
            return HttpNotFound();
        }

        return View(produtor);
    }

    // POST: Produtores/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteConfirmedAsync(int id)
    {
        var produtor = _context.Produtores.Find(id);

        if (produtor != null)
        {
            _context.Produtores.Remove(produtor);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }

    [HttpPost, ActionName("Upload")]
    public async Task<ActionResult> UploadAsync(ProdutorArquivoViewModel arquivo)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var produtor = await _context.Produtores
                                     .Include(x => x.Documentos)
                                     .FirstAsync(x => x.Id == arquivo.VinculoId);

        produtor.Documentos ??= new List<ProdutorArquivo>();

        produtor.Documentos.Add(arquivo.Model);
        _context.Produtores.Update(produtor);

        await _context.SaveChangesAsync();

        return View("_file-list", produtor);
    }

    [HttpPost, ActionName("DeleteFile")]
    public async Task<ActionResult> DeleteFileAsync(int id)
    {
        var arquivo = await _context.ProdutoresArquivos.FindAsync(id);

        if (arquivo == null)
        {
            return Problem();
        }

        var produtor = await _context.Produtores
                                     .Include(x => x.Documentos)
                                     .FirstAsync(x => x.Documentos!.Contains(arquivo));

        _context.ProdutoresArquivos.Remove(arquivo);
        await _context.SaveChangesAsync();

        return View("_file-list", produtor);
    }

    [HttpGet("Produtores/ViewFile/{id}")]
    public async Task<ActionResult> ViewFileAsync(int id)
    {
        var arquivo = await _context.ProdutoresArquivos.FindAsync(id);

        if (arquivo == null)
        {
            return Problem();
        }

        return File(arquivo.Dados, arquivo.ContentType);
    }
}