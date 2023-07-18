using System.Linq;
using Microsoft.AspNetCore.Mvc;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;
using System.Collections.Generic;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Repositories;

namespace GeotecnologiaKNS.Controllers
{
    public class PropriedadesController : Controller
    {
        private readonly IPropriedadeRepository _propriedadeRepository;

        public PropriedadesController(ApplicationDbContext context, IPropriedadeRepository propriedadeRepository)
        {
            _propriedadeRepository = propriedadeRepository;
        }

        // GET: Propriedades
        public ActionResult Index()
        {
            var propriedades = _propriedadeRepository.ObterTodasPropriedades();
            return View(propriedades);
        }

        // GET: Propriedades/Details/5
        public ActionResult Details(int id)
        {
            var propriedade = _propriedadeRepository.ObterPropriedadePorId(id);

            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        [HttpGet]
        public JsonResult CarregarMunicipios(string estado)
        {
            var municipiosPorEstado = MunicipiosPorEstado.Municipio;
            var municipios = municipiosPorEstado.FirstOrDefault(x => x.Key.ToString() == estado).Value;

            return Json(municipios);
        }

        // GET: Propriedades/Create
        public ActionResult Create()
        {
            var propriedade = new Propriedade();

            return View(propriedade);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Propriedade propriedade)
        {
            if (ModelState.IsValid)
            {
                if (ModelState.IsValid)
                {
                    _propriedadeRepository.CadastrarPropriedade(propriedade);

                    return RedirectToAction("Index");
                }
            }
            return View(propriedade);
        }

        public ActionResult Edit(int id)
        {
            var propriedade = _propriedadeRepository.ObterPropriedadePorId(id);

            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Propriedade propriedade)
        {
            if (ModelState.IsValid)
            {
                _propriedadeRepository.AtualizarPropriedade(propriedade);
                return RedirectToAction("Index");
            }

            return View(propriedade);
        }

        public ActionResult Delete(int id)
        {
            var propriedade = _propriedadeRepository.ObterPropriedadePorId(id);

            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var propriedade = _propriedadeRepository.ObterPropriedadePorId(id);

            if (propriedade != null)
            {
                _propriedadeRepository.RemoverPropriedade(propriedade);
            }

            return RedirectToAction("Index");
        }

      

        
    }
}
