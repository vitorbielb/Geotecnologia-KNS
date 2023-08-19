using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;

namespace GeotecnologiaKNS.Controllers;

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
    public async Task<ActionResult> CreateAsync(Produtor produtor)
    {
        if (ModelState.IsValid)
        {
            await _context.Produtores.AddAsync(produtor);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        return View(produtor);
    }

    // GET: Produtores/Edit/5
    public ActionResult Edit(int id)
    {
        var produtor = _context.Produtores.Find(id);

        if (produtor == null)
        {
            return HttpNotFound();
        }

        ViewBag.VinculoId = produtor.Id;
        return View(produtor);
    }

    // POST: Produtores/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> EditAsync(Produtor produtor)
    {
        if (ModelState.IsValid)
        {
            _context.Produtores.Update(produtor);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        return View(produtor);
    }

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
    public ActionResult Upload(int vinculoId, Arquivo arquivo)
    {
        var produtor = _context.Produtores.Find(vinculoId);
        produtor.Documentos ??= new List<Arquivo> { arquivo };
        return Ok();
    }
}



