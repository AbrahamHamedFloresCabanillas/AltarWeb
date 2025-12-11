using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltarWeb.Controllers
{
    public class EvaluacionController : Controller
    {
        private readonly AltarDbContext _context;
        public EvaluacionController(AltarDbContext context) { _context = context; }

        // GET: Mostrar formulario vacío
        public IActionResult Crear()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            return View(new Evaluacion()); // Modelo vacío
        }

        // POST: Recibir datos, calcular y guardar
        [HttpPost]
        public IActionResult Crear(Evaluacion eval, List<string> NombresIntegrantes, List<string> MatriculasIntegrantes)
        {
            // 1. Recuperar ID del Juez
            int? juezId = HttpContext.Session.GetInt32("JuezId");
            if (juezId == null) return RedirectToAction("Login", "Acceso");
            eval.JuezId = juezId.Value;

            // 2. Calcular Puntaje (Igual que en WinForms)
            int encontrados = 0;
            if (eval.ChkFoto) encontrados++;
            if (eval.ChkVelas) encontrados++;
            if (eval.ChkFlor) encontrados++;
            if (eval.ChkPapel) encontrados++;
            if (eval.ChkPan) encontrados++;
            if (eval.ChkAgua) encontrados++;
            if (eval.ChkSal) encontrados++;
            if (eval.ChkIncienso) encontrados++;
            if (eval.ChkCalaveritas) encontrados++;
            if (eval.ChkObjetos) encontrados++;

            decimal notaTrad = Math.Min(10, (encontrados + eval.NotaTradicion) / 2);
            decimal notaPers = Math.Min(10, eval.NotaPersonalizacion + (eval.BonusTematicos * 0.5m));

            eval.NotaFinal = (notaTrad * 0.3m) + (notaPers * 0.4m) + (eval.NotaEstetica * 0.3m);
            eval.Fecha = DateTime.Now;

            // 3. Guardar Integrantes
            if (NombresIntegrantes != null)
            {
                for (int i = 0; i < NombresIntegrantes.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(NombresIntegrantes[i]))
                    {
                        eval.Integrantes.Add(new Integrante { Nombre = NombresIntegrantes[i], Matricula = MatriculasIntegrantes[i] });
                    }
                }
            }

            // 4. Guardar en BD
            _context.Evaluaciones.Add(eval);
            _context.SaveChanges();

            return RedirectToAction("Detalle", new { id = eval.Id });
        }

        public IActionResult Detalle(int id)
        {
            var eval = _context.Evaluaciones
                .Include(e => e.Integrantes)
                .Include(e => e.Juez)
                .FirstOrDefault(e => e.Id == id);
            return View(eval); // Vista de solo lectura
        }
    }
}