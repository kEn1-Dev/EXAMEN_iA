using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using MonitorSerial.Models;
using MonitorSerial.Services;

namespace MonitorSerial.Controllers
{
    /// <summary>
    /// Parte 4.1, 4.2 y Parte 5.2, 5.3:
    /// Controlador de la entidad Lectura generado con Entity Framework,
    /// protegido con [Authorize] para consulta/registro y restringido con
    /// [Authorize(Roles = "Administrador")] para editar y eliminar.
    /// </summary>
    [Authorize]
    public class LecturasController : Controller
    {
        private MonitorSerialEntities db = new MonitorSerialEntities();

        // GET: Lecturas
        // Parte 4.2: El historial muestra las lecturas de la más reciente a la más antigua.
        public ActionResult Index()
        {
            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                var lecturas = db.Lectura
                    .Include(l => l.Dispositivo)
                    .OrderByDescending(l => l.FechaHora)
                    .ToList();
                return View(lecturas);
            }

            var estado = RepositorioRespaldoLocal.CargarEstado();
            return View(estado.Lecturas.OrderByDescending(l => l.FechaHora).ToList());
        }

        // GET: Lecturas/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Lectura lectura;
            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                lectura = db.Lectura
                    .Include(l => l.Dispositivo)
                    .FirstOrDefault(l => l.LecturaId == id);
            }
            else
            {
                var estado = RepositorioRespaldoLocal.CargarEstado();
                lectura = estado.Lecturas.FirstOrDefault(l => l.LecturaId == id.Value);
            }

            if (lectura == null)
            {
                return HttpNotFound();
            }
            return View(lectura);
        }

        // GET: Lecturas/Create
        public ActionResult Create()
        {
            CargarSelectDispositivos(null);
            var modelo = new Lectura
            {
                FechaHora = DateTime.Now
            };
            return View(modelo);
        }

        // POST: Lecturas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "LecturaId,DispositivoId,Temperatura,FechaHora")] Lectura lectura)
        {
            if (lectura.FechaHora == default(DateTime))
            {
                lectura.FechaHora = DateTime.Now;
            }

            if (ModelState.IsValid)
            {
                if (RepositorioRespaldoLocal.HaySqlServerDisponible())
                {
                    db.Lectura.Add(lectura);
                    db.SaveChanges();
                }
                else
                {
                    var estado = RepositorioRespaldoLocal.CargarEstado();
                    lectura.LecturaId = estado.Lecturas.Count > 0 ? estado.Lecturas.Max(l => l.LecturaId) + 1 : 1;
                    estado.Lecturas.Add(lectura);
                    RepositorioRespaldoLocal.GuardarEstado(estado);
                }
                return RedirectToAction("Index");
            }

            CargarSelectDispositivos(lectura.DispositivoId);
            return View(lectura);
        }

        // GET: Lecturas/Edit/5
        // Parte 5.3: Solo los usuarios con el rol Administrador pueden editar lecturas.
        [Authorize(Roles = "Administrador")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Lectura lectura;
            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                lectura = db.Lectura.Find(id);
            }
            else
            {
                var estado = RepositorioRespaldoLocal.CargarEstado();
                lectura = estado.Lecturas.FirstOrDefault(l => l.LecturaId == id.Value);
            }

            if (lectura == null)
            {
                return HttpNotFound();
            }
            CargarSelectDispositivos(lectura.DispositivoId);
            return View(lectura);
        }

        // POST: Lecturas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public ActionResult Edit([Bind(Include = "LecturaId,DispositivoId,Temperatura,FechaHora")] Lectura lectura)
        {
            if (ModelState.IsValid)
            {
                if (RepositorioRespaldoLocal.HaySqlServerDisponible())
                {
                    db.Entry(lectura).State = EntityState.Modified;
                    db.SaveChanges();
                }
                else
                {
                    var estado = RepositorioRespaldoLocal.CargarEstado();
                    var existente = estado.Lecturas.FirstOrDefault(l => l.LecturaId == lectura.LecturaId);
                    if (existente != null)
                    {
                        existente.DispositivoId = lectura.DispositivoId;
                        existente.Temperatura = lectura.Temperatura;
                        existente.FechaHora = lectura.FechaHora;
                        RepositorioRespaldoLocal.GuardarEstado(estado);
                    }
                }
                return RedirectToAction("Index");
            }
            CargarSelectDispositivos(lectura.DispositivoId);
            return View(lectura);
        }

        // GET: Lecturas/Delete/5
        // Parte 5.3: Solo los usuarios con el rol Administrador pueden eliminar lecturas.
        [Authorize(Roles = "Administrador")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Lectura lectura;
            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                lectura = db.Lectura
                    .Include(l => l.Dispositivo)
                    .FirstOrDefault(l => l.LecturaId == id);
            }
            else
            {
                var estado = RepositorioRespaldoLocal.CargarEstado();
                lectura = estado.Lecturas.FirstOrDefault(l => l.LecturaId == id.Value);
            }

            if (lectura == null)
            {
                return HttpNotFound();
            }
            return View(lectura);
        }

        // POST: Lecturas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public ActionResult DeleteConfirmed(int id)
        {
            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                Lectura lectura = db.Lectura.Find(id);
                if (lectura != null)
                {
                    db.Lectura.Remove(lectura);
                    db.SaveChanges();
                }
            }
            else
            {
                var estado = RepositorioRespaldoLocal.CargarEstado();
                estado.Lecturas.RemoveAll(l => l.LecturaId == id);
                RepositorioRespaldoLocal.GuardarEstado(estado);
            }
            return RedirectToAction("Index");
        }

        private void CargarSelectDispositivos(int? seleccionado)
        {
            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                ViewBag.DispositivoId = new SelectList(db.Dispositivo.Where(d => d.Activo), "DispositivoId", "Nombre", seleccionado);
            }
            else
            {
                var estado = RepositorioRespaldoLocal.CargarEstado();
                ViewBag.DispositivoId = new SelectList(estado.Dispositivos.Where(d => d.Activo), "DispositivoId", "Nombre", seleccionado);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
