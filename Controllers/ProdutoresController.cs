using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;

namespace GeotecnologiaKNS.Controllers;

public class ProdutoresController : Controller
{
    private readonly IProdutorRepository _produtorRepository;

    public ProdutoresController(IProdutorRepository produtorRepository)
    {
        _produtorRepository = produtorRepository;
    }

    // GET: Produtores
    public ActionResult Index()
    {
        var produtores = _produtorRepository.ObterTodos();
        return View(produtores);
    }

    // GET: Produtores/Details/5
    public ActionResult Details(int id)
    {
        var produtor = _produtorRepository.ObterPorId(id);

        if (produtor == null)
        {
            return HttpNotFound();
        }

        return View(produtor);
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
    public ActionResult Create(Produtor produtor)
    {
        if (ModelState.IsValid)
        {
            _produtorRepository.CadastrarProdutor(produtor);
            return RedirectToAction("Index");
        }

        return View(produtor);
    }

    // GET: Produtores/Edit/5
    public ActionResult Edit(int id)
    {
        var produtor = _produtorRepository.ObterPorId(id);

        if (produtor == null)
        {
            return HttpNotFound();
        }

        return View(produtor);
    }

    // POST: Produtores/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Edit(Produtor produtor)
    {
        if (ModelState.IsValid)
        {
            _produtorRepository.AtualizarProdutor(produtor);
            return RedirectToAction("Index");
        }

        return View(produtor);
    }

    // GET: Produtores/Delete/5
    public ActionResult Delete(int id)
    {
        var produtor = _produtorRepository.ObterPorId(id);

        if (produtor == null)
        {
            return HttpNotFound();
        }

        return View(produtor);
    }

    // POST: Produtores/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmed(int id)
    {
        var produtor = _produtorRepository.ObterPorId(id);

        if (produtor != null)
        {
            _produtorRepository.RemoverProdutor(produtor);
        }

        return RedirectToAction("Index");
    }
}



