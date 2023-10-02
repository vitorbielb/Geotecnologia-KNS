using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GeotecnologiaKNS.Controllers;
[Authorize]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;

    public AnalistaController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Solicitacoes
    public async Task<IActionResult> Index()
    {
        var solicitacoes = _context.Solicitacao.Include(s => s.Propriedade)
                                               .Include(s => s.Propriedade.Produtor);
        return View(await solicitacoes.ToListAsync());
    }

    

    // GET: Solicitacoes/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        ViewBag.Status = ((Status[])Enum.GetValues(typeof(Status)))
               .ToSelectListItems(
                   x => x.ToString(),
                   x => (int)x,
                   options => options.Placeholder = "Selecione...");

        if (id == null || _context.Solicitacao == null)
        {
            return NotFound();
        }

        var solicitacao = await _context.Solicitacao.FindAsync(id);
        if (solicitacao == null)
        {
            return NotFound();
        }
        ViewData["PropriedadeId"] = new SelectList(_context.Propriedades, "Id", "NomePropriedade", solicitacao.PropriedadeId);
        return View(solicitacao);
    }

    // POST: Solicitacoes/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [TenantFilter]
    public async Task<IActionResult> Edit([Bind("Id,PropriedadeId,Analista,Solicitante,DataSolicitacao,DataAnalise,Observacao,Status,Parecer")] Solicitacao solicitacao)
    {
        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(solicitacao);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SolicitacaoExists(solicitacao.Id))
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
        ViewData["PropriedadeId"] = new SelectList(_context.Propriedades, "Id", "NomePropriedade", solicitacao.PropriedadeId);
        return View(solicitacao);
    }

    private bool SolicitacaoExists(int id)
    {
        return (_context.Solicitacao?.Any(e => e.Id == id)).GetValueOrDefault();
    }
}

