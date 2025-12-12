using AltarWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AltarWeb.Controllers
{
    public class EvaluacionController : Controller
    {
        private readonly AltarDbContext _context;

        public EvaluacionController(AltarDbContext context)
        {
            _context = context;
        }

        // GET: Mostrar formulario vacío
        public IActionResult Crear()
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");
            return View(new Evaluacion());
        }

        // POST: Recibir datos, validar y guardar
        [HttpPost]
        public IActionResult Crear(Evaluacion eval, List<string> NombresIntegrantes, List<string> MatriculasIntegrantes)
        {
            // 1. Validar Sesión
            int? juezId = HttpContext.Session.GetInt32("JuezId");
            if (juezId == null) return RedirectToAction("Login", "Acceso");
            eval.JuezId = juezId.Value;

            // --- VALIDACIÓN 1: NOMBRE DE EQUIPO DUPLICADO ---
            // Buscamos si ya existe alguna evaluación con este nombre exacto
            bool equipoExiste = _context.Evaluaciones.Any(e => e.NombreEquipo == eval.NombreEquipo);

            if (equipoExiste)
            {
                ViewBag.Error = $"El nombre de equipo '{eval.NombreEquipo}' ya está registrado. Por favor elige otro nombre.";
                return View(eval); // Detenemos el guardado
            }

            // --- VALIDACIÓN 2: MATRÍCULAS DUPLICADAS ---
            if (MatriculasIntegrantes != null)
            {
                // A. Duplicados en la misma lista actual (escribió la misma dos veces)
                var duplicadosEnLista = MatriculasIntegrantes
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicadosEnLista.Any())
                {
                    ViewBag.Error = $"Error: Escribiste la matrícula '{duplicadosEnLista.First()}' dos veces en este mismo equipo.";
                    return View(eval);
                }

                // B. Duplicados en la Base de Datos (Ya inscrito en otro equipo)
                foreach (var mat in MatriculasIntegrantes)
                {
                    if (!string.IsNullOrWhiteSpace(mat))
                    {
                        var alumnoExistente = _context.Integrantes
                            .Include(i => i.Evaluacion)
                            .FirstOrDefault(i => i.Matricula == mat);

                        if (alumnoExistente != null)
                        {
                            ViewBag.Error = $"El alumno {alumnoExistente.Nombre} de matrícula {alumnoExistente.Matricula} ya está inscrito en el equipo {alumnoExistente.Evaluacion.NombreEquipo}.";
                            return View(eval);
                        }
                    }
                }
            }

            // 3. Calcular Puntaje
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

            // 4. Preparar Integrantes
            if (NombresIntegrantes != null)
            {
                for (int i = 0; i < NombresIntegrantes.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(NombresIntegrantes[i]))
                    {
                        eval.Integrantes.Add(new Integrante
                        {
                            Nombre = NombresIntegrantes[i],
                            Matricula = MatriculasIntegrantes[i]
                        });
                    }
                }
            }

            if (eval.Integrantes.Count == 0)
            {
                ViewBag.Error = "Debes agregar al menos un integrante al equipo.";
                return View(eval);
            }

            // 5. Guardar en BD
            if (ModelState.IsValid)
            {
                _context.Evaluaciones.Add(eval);
                _context.SaveChanges();
                return RedirectToAction("Detalle", new { id = eval.Id });
            }

            return View(eval);
        }

        // GET: Detalle (Consulta)
        public IActionResult Detalle(int id)
        {
            if (HttpContext.Session.GetInt32("JuezId") == null) return RedirectToAction("Login", "Acceso");

            var eval = _context.Evaluaciones
                .Include(e => e.Integrantes)
                .Include(e => e.Juez)
                .FirstOrDefault(e => e.Id == id);

            if (eval == null) return NotFound();

            return View(eval);
        }
    }
}