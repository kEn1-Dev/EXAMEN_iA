using System;
using System.ComponentModel.DataAnnotations;

namespace MonitorSerial.Models
{
    // Parte 4.3: Clase parcial vinculada a la clase de metadatos para no perder
    // las anotaciones al actualizar el modelo Database First (MonitorModel.edmx).
    [MetadataType(typeof(LecturaMetadata))]
    public partial class Lectura
    {
    }

    public class LecturaMetadata
    {
        [Required(ErrorMessage = "El dispositivo es obligatorio.")]
        [Display(Name = "Dispositivo")]
        public int DispositivoId { get; set; }

        [Required(ErrorMessage = "La temperatura es obligatoria.")]
        [Range(typeof(decimal), "-40", "125", ErrorMessage = "La temperatura debe estar en el rango de -40 a 125 °C.")]
        [Display(Name = "Temperatura (°C)")]
        public decimal Temperatura { get; set; }

        [Display(Name = "Fecha y hora")]
        [DataType(DataType.DateTime)]
        public DateTime FechaHora { get; set; }
    }
}
