using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using MonitorSerial.Models;
using Newtonsoft.Json;

namespace MonitorSerial.Services
{
    /// <summary>
    /// Provee persistencia local en disco (App_Data/MonitorSerial_Datos.json) como respaldo automático
    /// cuando el servicio de SQL Server no está instalado en el equipo local de pruebas.
    /// Si SQL Server está disponible, los controladores utilizan directamente MonitorSerialEntities (EF Database First).
    /// </summary>
    public static class RepositorioRespaldoLocal
    {
        private static readonly object _lockArchivo = new object();
        private static bool? _sqlServerDisponible;
        private static DateTime _ultimaComprobacionSql = DateTime.MinValue;

        public class UsuarioLocal
        {
            public string Id { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string Rol { get; set; }
        }

        public class EstadoBaseDatosLocal
        {
            public List<Dispositivo> Dispositivos { get; set; }
            public List<Lectura> Lecturas { get; set; }
            public List<UsuarioLocal> Usuarios { get; set; }
        }

        private static string ObtenerRutaArchivo()
        {
            string carpetaAppData = HostingEnvironment.MapPath("~/App_Data");
            if (string.IsNullOrEmpty(carpetaAppData))
            {
                carpetaAppData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            }
            if (!Directory.Exists(carpetaAppData))
            {
                Directory.CreateDirectory(carpetaAppData);
            }
            return Path.Combine(carpetaAppData, "MonitorSerial_Datos.json");
        }

        /// <summary>
        /// Verifica rápidamente (timeout 1 seg) si hay una instancia de SQL Server activa.
        /// </summary>
        public static bool HaySqlServerDisponible()
        {
            if (_sqlServerDisponible.HasValue && (DateTime.Now - _ultimaComprobacionSql).TotalSeconds < 30)
            {
                return _sqlServerDisponible.Value;
            }

            _ultimaComprobacionSql = DateTime.Now;
            try
            {
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = "localhost",
                    InitialCatalog = "MonitorSerial",
                    IntegratedSecurity = true,
                    ConnectTimeout = 1
                };
                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    conn.Open();
                    _sqlServerDisponible = true;
                    return true;
                }
            }
            catch
            {
                _sqlServerDisponible = false;
                return false;
            }
        }

        public static EstadoBaseDatosLocal CargarEstado()
        {
            lock (_lockArchivo)
            {
                string ruta = ObtenerRutaArchivo();
                if (File.Exists(ruta))
                {
                    try
                    {
                        string json = File.ReadAllText(ruta);
                        var estado = JsonConvert.DeserializeObject<EstadoBaseDatosLocal>(json);
                        if (estado != null && estado.Dispositivos != null && estado.Lecturas != null && estado.Usuarios != null)
                        {
                            ReconstruirNavegacion(estado);
                            return estado;
                        }
                    }
                    catch
                    {
                        // Si el archivo está corrupto, se regenera con los datos iniciales de script.sql
                    }
                }

                var inicial = CrearEstadoInicial();
                GuardarEstado(inicial);
                ReconstruirNavegacion(inicial);
                return inicial;
            }
        }

        public static void GuardarEstado(EstadoBaseDatosLocal estado)
        {
            lock (_lockArchivo)
            {
                string ruta = ObtenerRutaArchivo();
                // Evitar referencias circulares al serializar
                var copia = new EstadoBaseDatosLocal
                {
                    Dispositivos = estado.Dispositivos.Select(d => new Dispositivo
                    {
                        DispositivoId = d.DispositivoId,
                        Nombre = d.Nombre,
                        PuertoCOM = d.PuertoCOM,
                        Activo = d.Activo
                    }).ToList(),
                    Lecturas = estado.Lecturas.Select(l => new Lectura
                    {
                        LecturaId = l.LecturaId,
                        DispositivoId = l.DispositivoId,
                        Temperatura = l.Temperatura,
                        FechaHora = l.FechaHora
                    }).ToList(),
                    Usuarios = estado.Usuarios
                };

                string json = JsonConvert.SerializeObject(copia, Formatting.Indented);
                File.WriteAllText(ruta, json);
            }
        }

        private static void ReconstruirNavegacion(EstadoBaseDatosLocal estado)
        {
            var mapaDispositivos = estado.Dispositivos.ToDictionary(d => d.DispositivoId, d => d);
            foreach (var d in estado.Dispositivos)
            {
                d.Lectura = new List<Lectura>();
            }
            foreach (var l in estado.Lecturas)
            {
                Dispositivo disp;
                if (mapaDispositivos.TryGetValue(l.DispositivoId, out disp))
                {
                    l.Dispositivo = disp;
                    disp.Lectura.Add(l);
                }
            }
        }

        private static EstadoBaseDatosLocal CrearEstadoInicial()
        {
            var ahora = DateTime.Now;
            return new EstadoBaseDatosLocal
            {
                // Mismos dispositivos iniciales definidos en script.sql
                Dispositivos = new List<Dispositivo>
                {
                    new Dispositivo { DispositivoId = 1, Nombre = "Sensor Sala de Equipos Principal", PuertoCOM = "COM3", Activo = true },
                    new Dispositivo { DispositivoId = 2, Nombre = "Sensor Rack Respaldo", PuertoCOM = "COM1", Activo = true }
                },
                // Mismas lecturas iniciales definidas en script.sql
                Lecturas = new List<Lectura>
                {
                    new Lectura { LecturaId = 1, DispositivoId = 1, Temperatura = 22.50m, FechaHora = ahora.AddMinutes(-30) },
                    new Lectura { LecturaId = 2, DispositivoId = 1, Temperatura = 23.10m, FechaHora = ahora.AddMinutes(-15) },
                    new Lectura { LecturaId = 3, DispositivoId = 1, Temperatura = 21.85m, FechaHora = ahora }
                },
                // Mismos dos usuarios de prueba definidos en Startup.Auth.cs (Parte 5.4)
                Usuarios = new List<UsuarioLocal>
                {
                    new UsuarioLocal
                    {
                        Id = "usr-admin-01",
                        Email = "admin@monitorserial.local",
                        Password = "DemoLab2026",
                        Rol = "Administrador"
                    },
                    new UsuarioLocal
                    {
                        Id = "usr-operador-02",
                        Email = "operador@monitorserial.local",
                        Password = "DemoLab2026",
                        Rol = "Operador"
                    }
                }
            };
        }
    }
}
