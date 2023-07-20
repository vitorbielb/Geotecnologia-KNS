using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;

namespace KNS.Controllers
{
    public class PropriedadesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PropriedadesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Propriedades
        public async Task<IActionResult> Index()
        {
            return _context.Propriedade != null ?
                        View(await _context.Propriedade.ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Propriedade'  is null.");
        }

        // GET: Propriedades/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Propriedade == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedade
                .FirstOrDefaultAsync(m => m.Id == id);
            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        // GET: Propriedades/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Propriedades/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,NomePropriedade,TipoPropriedade,CicloProducao,Area,AreaUtil,Latitude,Longitude,OrigemCoordenadas,Bioma,UnidadeFederativa,Municipio,Industria,TipoCadastroRural,Matricula,CadastroAmbientalRural,LicencaAmbiental,Ccir,Incra,Outros")] Propriedade propriedade)
        {
            if (ModelState.IsValid)
            {
                _context.Add(propriedade);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(propriedade);
        }

        // GET: Propriedades/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Propriedade == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedade.FindAsync(id);
            if (propriedade == null)
            {
                return NotFound();
            }
            return View(propriedade);
        }

        // POST: Propriedades/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,NomePropriedade,TipoPropriedade,CicloProducao,Area,AreaUtil,Latitude,Longitude,OrigemCoordenadas,Bioma,UnidadeFederativa,Municipio,Industria,TipoCadastroRural,Matricula,CadastroAmbientalRural,LicencaAmbiental,Ccir,Incra,Outros")] Propriedade propriedade)
        {
            if (id != propriedade.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(propriedade);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PropriedadeExists(propriedade.Id))
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
            return View(propriedade);
        }

        // GET: Propriedades/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Propriedade == null)
            {
                return NotFound();
            }

            var propriedade = await _context.Propriedade
                .FirstOrDefaultAsync(m => m.Id == id);
            if (propriedade == null)
            {
                return NotFound();
            }

            return View(propriedade);
        }

        // POST: Propriedades/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Propriedade == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Propriedade'  is null.");
            }
            var propriedade = await _context.Propriedade.FindAsync(id);
            if (propriedade != null)
            {
                _context.Propriedade.Remove(propriedade);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public ActionResult GetCitiesByUF(Estado uf)
        {
            return Json(UnidadesFederativasExtension.GetCities(uf));
        }

        private bool PropriedadeExists(int id)
        {
            return (_context.Propriedade?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}