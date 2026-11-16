using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using MonitorSerial.Models;
using MonitorSerial.Services;

namespace MonitorSerial.Controllers
{
    /// <summary>
    /// Parte 3.3 y Parte 5.2: Controlador protegido con [Authorize] para vigilar la temperatura
    /// mediante una instancia estática de ServicioSerial protegida con lock.
    /// </summary>
    [Authorize]
    public class SensorController : Controller
    {
        // Instancia estática del servicio serial y objeto de sincronización para lock
        private static readonly ServicioSerial _servicioSerial = new ServicioSerial();
        private static readonly object _bloqueoSerial = new object();

        // GET: Sensor
        public ActionResult Index()
        {
            string[] puertosDisponibles;
            lock (_bloqueoSerial)
            {
                puertosDisponibles = _servicioSerial.ListarPuertos();
            }

            ViewBag.Puertos = puertosDisponibles;

            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                using (var db = new MonitorSerialEntities())
                {
                    var dispositivos = db.Dispositivo
                        .Where(d => d.Activo)
                        .OrderBy(d => d.Nombre)
                        .ToList();

                    ViewBag.Dispositivos = dispositivos;

                    var ultimaLectura = db.Lectura
                        .Include(l => l.Dispositivo)
                        .OrderByDescending(l => l.FechaHora)
                        .FirstOrDefault();

                    ViewBag.UltimaLectura = ultimaLectura;
                }
            }
            else
            {
                var estado = RepositorioRespaldoLocal.CargarEstado();
                ViewBag.Dispositivos = estado.Dispositivos
                    .Where(d => d.Activo)
                    .OrderBy(d => d.Nombre)
                    .ToList();
                ViewBag.UltimaLectura = estado.Lecturas
                    .OrderByDescending(l => l.FechaHora)
                    .FirstOrDefault();
            }

            return View();
        }

        // POST: Sensor/Leer
        // Parte 3.3: Acción Leer (POST) que usa una instancia estática del servicio protegida con lock,
        // guarda la lectura con Entity Framework dentro de un bloque using y responde en JSON,
        // contemplando los casos de error.
        [HttpPost]
        public JsonResult Leer(int? dispositivoId, string puertoCom)
        {
            if (!dispositivoId.HasValue || dispositivoId.Value <= 0)
            {
                return Json(new
                {
                    exito = false,
                    mensaje = "Debe seleccionar un dispositivo válido antes de realizar la lectura."
                });
            }

            try
            {
                bool usarSql = RepositorioRespaldoLocal.HaySqlServerDisponible();
                Dispositivo dispositivo = null;

                if (usarSql)
                {
                    using (var db = new MonitorSerialEntities())
                    {
                        dispositivo = db.Dispositivo.FirstOrDefault(d => d.DispositivoId == dispositivoId.Value && d.Activo);
                    }
                }
                else
                {
                    var estado = RepositorioRespaldoLocal.CargarEstado();
                    dispositivo = estado.Dispositivos.FirstOrDefault(d => d.DispositivoId == dispositivoId.Value && d.Activo);
                }

                if (dispositivo == null)
                {
                    return Json(new
                    {
                        exito = false,
                        mensaje = "El dispositivo seleccionado no existe o se encuentra inactivo."
                    });
                }

                // Determinar el puerto COM a consultar (el enviado en la vista o el configurado en el dispositivo)
                string puertoObjetivo = !string.IsNullOrWhiteSpace(puertoCom)
                    ? puertoCom.Trim()
                    : dispositivo.PuertoCOM;

                decimal? temperaturaLeida;
                string detalleErrorSerial;

                // Lectura en el puerto serie protegida con lock sobre la instancia estática
                lock (_bloqueoSerial)
                {
                    temperaturaLeida = _servicioSerial.LeerTemperatura(puertoObjetivo);
                    detalleErrorSerial = _servicioSerial.UltimoError;
                }

                if (!temperaturaLeida.HasValue)
                {
                    return Json(new
                    {
                        exito = false,
                        mensaje = !string.IsNullOrWhiteSpace(detalleErrorSerial)
                            ? detalleErrorSerial
                            : string.Format("No se recibió un dato válido desde el puerto {0} o venció el tiempo de espera.", puertoObjetivo)
                    });
                }

                // Validar que la temperatura esté en el rango permitido (-40 a 125 °C)
                if (temperaturaLeida.Value < -40m || temperaturaLeida.Value > 125m)
                {
                    return Json(new
                    {
                        exito = false,
                        mensaje = string.Format("La temperatura leída ({0:0.00} °C) está fuera del rango válido (-40 a 125 °C).", temperaturaLeida.Value)
                    });
                }

                Lectura nuevaLectura;
                if (usarSql)
                {
                    // Guardar la lectura con Entity Framework dentro de un bloque using
                    using (var db = new MonitorSerialEntities())
                    {
                        nuevaLectura = new Lectura
                        {
                            DispositivoId = dispositivo.DispositivoId,
                            Temperatura = temperaturaLeida.Value,
                            FechaHora = DateTime.Now
                        };

                        db.Lectura.Add(nuevaLectura);
                        db.SaveChanges();
                    }
                }
                else
                {
                    var estado = RepositorioRespaldoLocal.CargarEstado();
                    int siguienteId = estado.Lecturas.Count > 0 ? estado.Lecturas.Max(l => l.LecturaId) + 1 : 1;
                    nuevaLectura = new Lectura
                    {
                        LecturaId = siguienteId,
                        DispositivoId = dispositivo.DispositivoId,
                        Temperatura = temperaturaLeida.Value,
                        FechaHora = DateTime.Now
                    };
                    estado.Lecturas.Add(nuevaLectura);
                    RepositorioRespaldoLocal.GuardarEstado(estado);
                }

                return Json(new
                {
                    exito = true,
                    lecturaId = nuevaLectura.LecturaId,
                    dispositivo = dispositivo.Nombre,
                    puerto = puertoObjetivo,
                    temperatura = nuevaLectura.Temperatura.ToString("0.00"),
                    fechaHora = nuevaLectura.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"),
                    mensaje = string.Format("Lectura registrada correctamente: {0:0.00} °C en {1} ({2}).", nuevaLectura.Temperatura, dispositivo.Nombre, puertoObjetivo)
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    exito = false,
                    mensaje = string.Format("Error al procesar o guardar la lectura: {0}", ex.Message)
                });
            }
        }
    }
}
