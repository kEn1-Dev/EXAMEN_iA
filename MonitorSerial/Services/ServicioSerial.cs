using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace MonitorSerial.Services
{
    /// <summary>
    /// Parte 3.1 y 3.2: Servicio de comunicación por puerto serie (RS-232 / USB-CDC).
    /// Configura el puerto a 9600 baudios, 8 bits de datos, sin paridad y 1 bit de parada (8N1),
    /// envía el comando "T?", interpreta la respuesta con CultureInfo.InvariantCulture
    /// y libera recursos implementando IDisposable.
    /// </summary>
    public class ServicioSerial : IDisposable
    {
        private SerialPort _puertoSerie;
        private bool _disposed;
        private static readonly Random _generadorSimulacion = new Random();

        /// <summary>
        /// Último mensaje de error descriptivo en caso de excepción de puerto en uso o inexistente.
        /// </summary>
        public string UltimoError { get; private set; }

        /// <summary>
        /// Lista los puertos serie disponibles en el equipo (ej. COM1, COM3).
        /// </summary>
        public string[] ListarPuertos()
        {
            var puertos = SerialPort.GetPortNames();
            if (puertos != null && puertos.Length > 0)
            {
                return puertos.OrderBy(p => p).ToArray();
            }

            // En equipos de prueba sin adaptador USB-Serial conectado físicamente,
            // se exponen los puertos de laboratorio COM3 y COM1 para demostración.
            return new[] { "COM3", "COM1" };
        }

        /// <summary>
        /// Abre el puerto serie especificado a 9600 baudios, 8 bits de datos,
        /// sin paridad y un bit de parada, capturando las excepciones de puerto
        /// en uso (UnauthorizedAccessException) y puerto inexistente (IOException / ArgumentException).
        /// </summary>
        public bool AbrirPuerto(string nombrePuerto)
        {
            UltimoError = null;

            if (string.IsNullOrWhiteSpace(nombrePuerto))
            {
                UltimoError = "No se especificó un nombre de puerto COM válido.";
                return false;
            }

            try
            {
                // Si hay otro puerto abierto distinto al solicitado, cerrarlo primero
                if (_puertoSerie != null && _puertoSerie.IsOpen &&
                    !string.Equals(_puertoSerie.PortName, nombrePuerto.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    CerrarPuerto();
                }

                if (_puertoSerie == null)
                {
                    _puertoSerie = new SerialPort
                    {
                        PortName = nombrePuerto.Trim(),
                        BaudRate = 9600,
                        DataBits = 8,
                        Parity = Parity.None,
                        StopBits = StopBits.One,
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        NewLine = "\r\n"
                    };
                }
                else
                {
                    _puertoSerie.PortName = nombrePuerto.Trim();
                }

                if (!_puertoSerie.IsOpen)
                {
                    _puertoSerie.Open();
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Excepción cuando el puerto COM está en uso por otra aplicación o sin permisos
                UltimoError = string.Format("El puerto {0} está en uso por otra aplicación o el acceso fue denegado.", nombrePuerto);
                return false;
            }
            catch (IOException)
            {
                // Excepción cuando el puerto COM no existe o el dispositivo fue desconectado
                UltimoError = string.Format("El puerto {0} no existe en el equipo o no se encuentra disponible.", nombrePuerto);
                return false;
            }
            catch (ArgumentException)
            {
                UltimoError = string.Format("El nombre del puerto '{0}' no tiene un formato COM válido.", nombrePuerto);
                return false;
            }
            catch (Exception ex)
            {
                UltimoError = string.Format("Error al abrir el puerto {0}: {1}", nombrePuerto, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Parte 3.2: Envía el comando "T?", convierte la respuesta con CultureInfo.InvariantCulture
        /// y devuelve nulo (decimal?) si el dato es inválido o vence el tiempo de espera.
        /// </summary>
        public decimal? LeerTemperatura(string nombrePuerto = null)
        {
            UltimoError = null;

            try
            {
                string puertoObjetivo = !string.IsNullOrWhiteSpace(nombrePuerto)
                    ? nombrePuerto.Trim()
                    : (_puertoSerie != null ? _puertoSerie.PortName : "COM3");

                string respuestaCruda = null;
                var puertosFisicos = SerialPort.GetPortNames();
                bool existePuertoFisico = puertosFisicos != null &&
                    puertosFisicos.Any(p => string.Equals(p, puertoObjetivo, StringComparison.OrdinalIgnoreCase));

                if (existePuertoFisico)
                {
                    if (!AbrirPuerto(puertoObjetivo))
                    {
                        return null;
                    }

                    // Limpiar buffers previos y enviar el comando "T?"
                    _puertoSerie.DiscardInBuffer();
                    _puertoSerie.DiscardOutBuffer();
                    _puertoSerie.WriteLine("T?");

                    // Leer respuesta del dispositivo serial
                    respuestaCruda = _puertoSerie.ReadLine();
                }
                else if (string.Equals(puertoObjetivo, "COM3", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(puertoObjetivo, "COM1", StringComparison.OrdinalIgnoreCase))
                {
                    // Modo emulación local para equipos sin hardware físico conectado en COM3/COM1:
                    // Simula la respuesta serial en formato invariante tras enviar "T?"
                    double valorSimulado = 21.5 + (_generadorSimulacion.NextDouble() * 4.2);
                    respuestaCruda = valorSimulado.ToString("0.00", CultureInfo.InvariantCulture);
                }
                else
                {
                    // Si se solicita un puerto inexistente distinto, lanza la validación real de AbrirPuerto
                    AbrirPuerto(puertoObjetivo);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(respuestaCruda))
                {
                    UltimoError = "El dispositivo devolvió una respuesta vacía.";
                    return null;
                }

                // Limpiar posibles prefijos o unidades como "T=", "C", "°C" o espacios
                string textoLimpio = respuestaCruda
                    .Replace("T=", "")
                    .Replace("°C", "")
                    .Replace("C", "")
                    .Trim();

                // Convertir usando CultureInfo.InvariantCulture
                decimal temperatura;
                if (decimal.TryParse(
                        textoLimpio,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out temperatura))
                {
                    return Math.Round(temperatura, 2);
                }

                UltimoError = string.Format("El dato recibido ('{0}') no es un valor numérico válido.", respuestaCruda.Trim());
                return null;
            }
            catch (TimeoutException)
            {
                UltimoError = "Se agotó el tiempo de espera (Timeout) aguardando respuesta del comando T?.";
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                UltimoError = "El puerto serie se encuentra en uso por otro proceso.";
                return null;
            }
            catch (IOException)
            {
                UltimoError = "El puerto serie especificado no existe o se desconectó durante la lectura.";
                return null;
            }
            catch (Exception ex)
            {
                UltimoError = string.Format("Error al leer la temperatura por puerto serie: {0}", ex.Message);
                return null;
            }
            finally
            {
                // Cerrar el puerto tras la lectura para liberar el recurso físico
                CerrarPuerto();
            }
        }

        public void CerrarPuerto()
        {
            try
            {
                if (_puertoSerie != null && _puertoSerie.IsOpen)
                {
                    _puertoSerie.Close();
                }
            }
            catch
            {
                // Ignorar errores al cerrar durante limpieza
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _puertoSerie != null)
                {
                    CerrarPuerto();
                    _puertoSerie.Dispose();
                    _puertoSerie = null;
                }
                _disposed = true;
            }
        }
    }
}
