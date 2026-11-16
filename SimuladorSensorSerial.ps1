<#
.SYNOPSIS
    Simulador de sensor de temperatura por puerto serie (COM) para probar MonitorSerial sin Arduino físico.
.DESCRIPTION
    Si instalas un par de puertos virtuales (por ejemplo con com0com: COM3 <-> COM4),
    ejecuta este script escuchando en COM4 mientras la aplicación web MonitorSerial
    consulta COM3 a 9600 baudios, 8N1.
    Cada vez que la aplicación web envía "T?", este script responde con una temperatura
    decimal en formato invariante (ej. "23.45").
#>
param(
    [string]$PuertoEscucha = "COM4"
)

Write-Host "=== Simulador de Sensor de Temperatura (MonitorSerial) ===" -ForegroundColor Cyan
Write-Host "Abriendo puerto $PuertoEscucha (9600 baudios, 8N1)..." -ForegroundColor Yellow

$port = New-Object System.IO.Ports.SerialPort($PuertoEscucha, 9600, [System.IO.Ports.Parity]::None, 8, [System.IO.Ports.StopBits]::One)
$port.NewLine = "`r`n"
$port.ReadTimeout = 5000

try {
    $port.Open()
    Write-Host "Escuchando comandos 'T?' en $PuertoEscucha. Presiona Ctrl+C para detener." -ForegroundColor Green
    $rnd = New-Object System.Random

    while ($true) {
        try {
            $linea = $port.ReadLine().Trim()
            if ($linea -eq "T?") {
                $temp = [Math]::Round(21.0 + ($rnd.NextDouble() * 5.0), 2)
                $respuesta = $temp.ToString("0.00", [System.Globalization.CultureInfo]::InvariantCulture)
                $port.WriteLine($respuesta)
                Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Recibido 'T?' -> Enviado: $respuesta °C" -ForegroundColor Green
            }
        }
        catch [System.TimeoutException] {
            # Continuar esperando comando T?
        }
    }
}
catch {
    Write-Host "No se pudo abrir el puerto $PuertoEscucha : $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($port.IsOpen) { $port.Close() }
    $port.Dispose()
}
