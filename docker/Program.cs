using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Inicializar SQL Server en contenedor automáticamente al arrancar
_ = Task.Run(() => BaseDatosDocker.InicializarSqlConReintentosAsync());

var bloqueoSerial = new object();
var generadorSim = new Random();

// Redirección raíz -> /Sensor
app.MapGet("/", (HttpContext ctx) => Results.Redirect("/Sensor"));

// ==================== ACCOUNT: LOGIN / REGISTER / LOGOFF ====================
app.MapGet("/Account/Login", (HttpContext ctx, string returnUrl = null) =>
{
    string html = PlantillaHtml.RenderLayout("Iniciar sesión", ctx.User, $@"
<div class='row'>
    <div class='col s12 m6 offset-m3'>
        <div class='card white z-depth-2'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Iniciar Sesión en MonitorSerial</span>
                <p class='grey-text text-darken-1'>Ingrese con una cuenta registrada para vigilar la temperatura de la sala de equipos.</p>
                <form method='post' action='/Account/Login'>
                    <input type='hidden' name='returnUrl' value='{WebUtility.HtmlEncode(returnUrl ?? "/Sensor")}' />
                    <div class='input-field section'>
                        <input id='Email' name='Email' type='email' class='validate' required />
                        <label for='Email' class='active'>Correo electrónico</label>
                    </div>
                    <div class='input-field section'>
                        <input id='Password' name='Password' type='password' class='validate' required />
                        <label for='Password' class='active'>Contraseña</label>
                    </div>
                    <p>
                        <label>
                            <input type='checkbox' name='RememberMe' value='true' />
                            <span>Recordar mi sesión</span>
                        </label>
                    </p>
                    <div class='section' style='margin-top:20px;'>
                        <button type='submit' class='btn waves-effect waves-light teal darken-2'>Iniciar sesión</button>
                        <a href='/Account/Register' class='btn-flat teal-text text-darken-3'>Registrar nuevo usuario</a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/Account/Login", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    string email = form["Email"].ToString().Trim();
    string password = form["Password"].ToString();
    string returnUrl = form["returnUrl"].ToString();

    var usuario = BaseDatosDocker.ValidarUsuario(email, password);
    if (usuario == null)
    {
        string htmlError = PlantillaHtml.RenderLayout("Iniciar sesión", ctx.User, $@"
<div class='row'>
    <div class='col s12 m6 offset-m3'>
        <div class='card white z-depth-2'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Iniciar Sesión en MonitorSerial</span>
                <p class='red-text text-darken-3'><strong>Intento de inicio de sesión no válido. Verifique su correo y contraseña.</strong></p>
                <form method='post' action='/Account/Login'>
                    <div class='input-field section'>
                        <input id='Email' name='Email' type='email' value='{WebUtility.HtmlEncode(email)}' required />
                        <label for='Email' class='active'>Correo electrónico</label>
                    </div>
                    <div class='input-field section'>
                        <input id='Password' name='Password' type='password' required />
                        <label for='Password' class='active'>Contraseña</label>
                    </div>
                    <div class='section'>
                        <button type='submit' class='btn waves-effect waves-light teal darken-2'>Iniciar sesión</button>
                        <a href='/Account/Register' class='btn-flat teal-text text-darken-3'>Registrar nuevo usuario</a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>");
        return Results.Content(htmlError, "text/html; charset=utf-8");
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, usuario.Email),
        new Claim(ClaimTypes.Role, usuario.Rol)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Sensor" : returnUrl);
});

app.MapGet("/Account/Register", (HttpContext ctx) =>
{
    string html = PlantillaHtml.RenderLayout("Registro de usuario", ctx.User, @"
<div class='row'>
    <div class='col s12 m6 offset-m3'>
        <div class='card white z-depth-2'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Registrar Nueva Cuenta</span>
                <p class='grey-text text-darken-1'>Las cuentas nuevas se crean con perfil estándar (consulta y registro de lecturas).</p>
                <form method='post' action='/Account/Register'>
                    <div class='input-field section'>
                        <input id='Email' name='Email' type='email' class='validate' required />
                        <label for='Email' class='active'>Correo electrónico</label>
                    </div>
                    <div class='input-field section'>
                        <input id='Password' name='Password' type='password' minlength='6' class='validate' required />
                        <label for='Password' class='active'>Contraseña</label>
                    </div>
                    <div class='input-field section'>
                        <input id='ConfirmPassword' name='ConfirmPassword' type='password' minlength='6' class='validate' required />
                        <label for='ConfirmPassword' class='active'>Confirmar contraseña</label>
                    </div>
                    <div class='section' style='margin-top:20px;'>
                        <button type='submit' class='btn waves-effect waves-light teal darken-2'>Registrarse</button>
                        <a href='/Account/Login' class='btn-flat'>Ya tengo cuenta</a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/Account/Register", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    string email = form["Email"].ToString().Trim();
    string password = form["Password"].ToString();
    string confirm = form["ConfirmPassword"].ToString();

    if (string.IsNullOrWhiteSpace(email) || password.Length < 6 || password != confirm)
    {
        return Results.Redirect("/Account/Register");
    }

    var nuevo = BaseDatosDocker.RegistrarUsuario(email, password);
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, nuevo.Email),
        new Claim(ClaimTypes.Role, nuevo.Rol)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/Sensor");
});

app.MapPost("/Account/LogOff", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Account/Login");
});

// ==================== SENSOR: MONITOR Y LECTURA AJAX ====================
app.MapGet("/Sensor", (HttpContext ctx) =>
{
    if (!ctx.User.Identity.IsAuthenticated)
        return Results.Redirect("/Account/Login?ReturnUrl=%2FSensor");

    var dispositivos = BaseDatosDocker.ObtenerDispositivosActivos();
    var lecturas = BaseDatosDocker.ObtenerLecturasOrdenadas();
    var ultima = lecturas.FirstOrDefault();
    var puertos = new[] { "COM3", "COM1" };

    string opcionesDisp = string.Join("", dispositivos.Select(d =>
        $"<option value='{d.DispositivoId}' data-puerto='{d.PuertoCOM}'>{WebUtility.HtmlEncode(d.Nombre)} (Puerto asignado: {WebUtility.HtmlEncode(d.PuertoCOM)})</option>"));

    string listaPuertos = string.Join("", puertos.Select(p =>
        $"<li class='collection-item'><strong>{p}</strong> — Parámetros: 9600 baudios, 8 bits, sin paridad, 1 bit de parada (8N1)</li>"));

    string opcionesPuertos = "<option value=''>Usar puerto predeterminado del dispositivo</option>" +
        string.Join("", puertos.Select(p => $"<option value='{p}'>{p} (Detectado en el sistema)</option>"));

    string tempInicial = ultima != null ? ultima.Temperatura.ToString("0.00", CultureInfo.InvariantCulture) + " °C" : "--.-- °C";
    string detalleInicial = ultima != null
        ? $"<span>Última lectura: <strong>{WebUtility.HtmlEncode(ultima.NombreDispositivo)}</strong> ({WebUtility.HtmlEncode(ultima.PuertoCOM)}) — {ultima.FechaHora:dd/MM/yyyy HH:mm:ss}</span>"
        : "<span>Sin lecturas registradas aún.</span>";

    string body = $@"
<div class='row'>
    <div class='col s12'>
        <h4 class='teal-text text-darken-3'>Vigilancia de Temperatura - Sala de Equipos</h4>
        <p class='grey-text text-darken-1'>
            Seleccione el dispositivo registrado, verifique el puerto serie COM y presione <strong>«Actualizar»</strong> para consultar la temperatura actual mediante el comando <code>T?</code> sin recargar la página.
        </p>
    </div>
</div>
<div class='row'>
    <div class='col s12 m6'>
        <div class='card white z-depth-2'>
            <div class='card-content center-align'>
                <span class='card-title teal-text text-darken-3'>Temperatura Actual</span>
                <div id='valorTemperatura' class='temperatura-display'>{tempInicial}</div>
                <p id='detalleLectura' class='grey-text text-darken-2'>{detalleInicial}</p>
            </div>
            <div class='card-action center-align'>
                <a href='/Lecturas' class='teal-text text-darken-3'>Ver Historial Completo</a>
            </div>
        </div>
        <div class='card white z-depth-1'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Puertos Serie Detectados en el Equipo</span>
                <ul class='collection'>{listaPuertos}</ul>
            </div>
        </div>
    </div>
    <div class='col s12 m6'>
        <div class='card white z-depth-2'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Consulta por Puerto Serie</span>
                <div class='section'>
                    <label for='selectDispositivo' class='black-text'><strong>1. Selector de Dispositivo</strong></label>
                    <select id='selectDispositivo' class='browser-default'>{opcionesDisp}</select>
                </div>
                <div class='section'>
                    <label for='selectPuerto' class='black-text'><strong>2. Puerto COM a Consultar</strong></label>
                    <select id='selectPuerto' class='browser-default'>{opcionesPuertos}</select>
                </div>
                <div class='section center-align' style='margin-top: 20px;'>
                    <button id='btnActualizar' type='button' class='btn-large waves-effect waves-light teal darken-2'>Actualizar</button>
                </div>
                <div id='mensajeEstado' class='card-panel hide' style='margin-top: 20px;'>
                    <span id='textoMensaje'></span>
                </div>
            </div>
        </div>
    </div>
</div>
<script>
document.addEventListener('DOMContentLoaded', function () {{
    var btnActualizar = document.getElementById('btnActualizar');
    var selectDispositivo = document.getElementById('selectDispositivo');
    var selectPuerto = document.getElementById('selectPuerto');
    var valorTemperatura = document.getElementById('valorTemperatura');
    var detalleLectura = document.getElementById('detalleLectura');
    var mensajeEstado = document.getElementById('mensajeEstado');
    var textoMensaje = document.getElementById('textoMensaje');

    function mostrarMensaje(exito, texto) {{
        mensajeEstado.className = exito
            ? 'card-panel green lighten-4 green-text text-darken-4'
            : 'card-panel red lighten-4 red-text text-darken-4';
        textoMensaje.textContent = texto;
        if (typeof M !== 'undefined' && M.toast) {{
            M.toast({{ html: texto, classes: exito ? 'green darken-2' : 'red darken-2' }});
        }}
    }}

    btnActualizar.addEventListener('click', function () {{
        btnActualizar.disabled = true;
        btnActualizar.textContent = 'Consultando...';
        $.ajax({{
            url: '/Sensor/Leer',
            type: 'POST',
            dataType: 'json',
            data: {{ dispositivoId: selectDispositivo.value, puertoCom: selectPuerto.value }},
            success: function (respuesta) {{
                if (respuesta && respuesta.exito) {{
                    valorTemperatura.textContent = respuesta.temperatura + ' °C';
                    detalleLectura.innerHTML = 'Última lectura: <strong>' + respuesta.dispositivo + '</strong> (' + respuesta.puerto + ') — ' + respuesta.fechaHora;
                    mostrarMensaje(true, respuesta.mensaje);
                }} else {{
                    mostrarMensaje(false, (respuesta && respuesta.mensaje) ? respuesta.mensaje : 'Error al consultar sensor.');
                }}
            }},
            error: function () {{
                mostrarMensaje(false, 'Error de comunicación con el servidor.');
            }},
            complete: function () {{
                btnActualizar.disabled = false;
                btnActualizar.textContent = 'Actualizar';
            }}
        }});
    }});
}});
</script>";

    return Results.Content(PlantillaHtml.RenderLayout("Monitor de Temperatura", ctx.User, body), "text/html; charset=utf-8");
});

app.MapPost("/Sensor/Leer", async (HttpContext ctx) =>
{
    if (!ctx.User.Identity.IsAuthenticated)
        return Results.Json(new { exito = false, mensaje = "Sesión no iniciada." });

    var form = await ctx.Request.ReadFormAsync();
    int.TryParse(form["dispositivoId"], out int dispositivoId);
    string puertoCom = form["puertoCom"].ToString().Trim();

    var disp = BaseDatosDocker.ObtenerDispositivosActivos().FirstOrDefault(d => d.DispositivoId == dispositivoId);
    if (disp == null)
        return Results.Json(new { exito = false, mensaje = "El dispositivo seleccionado no existe o está inactivo." });

    string puertoObjetivo = !string.IsNullOrWhiteSpace(puertoCom) ? puertoCom : disp.PuertoCOM;

    decimal temperaturaLeida;
    lock (bloqueoSerial)
    {
        if (!string.Equals(puertoObjetivo, "COM3", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(puertoObjetivo, "COM1", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new
            {
                exito = false,
                mensaje = $"El puerto {puertoObjetivo} no existe en el equipo o no se encuentra disponible."
            });
        }

        double simulado = 21.5 + (generadorSim.NextDouble() * 4.0);
        string tramaRespuesta = simulado.ToString("0.00", CultureInfo.InvariantCulture);
        decimal.TryParse(tramaRespuesta, NumberStyles.Number, CultureInfo.InvariantCulture, out temperaturaLeida);
    }

    var nueva = BaseDatosDocker.InsertarLectura(disp.DispositivoId, Math.Round(temperaturaLeida, 2), DateTime.Now);

    return Results.Json(new
    {
        exito = true,
        lecturaId = nueva.LecturaId,
        dispositivo = disp.Nombre,
        puerto = puertoObjetivo,
        temperatura = nueva.Temperatura.ToString("0.00", CultureInfo.InvariantCulture),
        fechaHora = nueva.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"),
        mensaje = $"Lectura registrada correctamente: {nueva.Temperatura.ToString("0.00", CultureInfo.InvariantCulture)} °C en {disp.Nombre} ({puertoObjetivo})."
    });
});

// ==================== LECTURAS: HISTORIAL Y CRUD ====================
app.MapGet("/Lecturas", (HttpContext ctx) =>
{
    if (!ctx.User.Identity.IsAuthenticated)
        return Results.Redirect("/Account/Login?ReturnUrl=%2FLecturas");

    bool esAdmin = ctx.User.IsInRole("Administrador");
    var lecturas = BaseDatosDocker.ObtenerLecturasOrdenadas();

    string filas = string.Join("", lecturas.Select(item =>
    {
        string botonesAdmin = esAdmin
            ? $" <a href='/Lecturas/Edit/{item.LecturaId}' class='btn-small waves-effect waves-light amber darken-3'>Editar</a> <a href='/Lecturas/Delete/{item.LecturaId}' class='btn-small waves-effect waves-light red darken-2'>Eliminar</a>"
            : "";
        return $@"
        <tr>
            <td>{item.LecturaId}</td>
            <td>{WebUtility.HtmlEncode(item.NombreDispositivo)}</td>
            <td>{WebUtility.HtmlEncode(item.PuertoCOM)}</td>
            <td><strong>{item.Temperatura.ToString("0.00", CultureInfo.InvariantCulture)} °C</strong></td>
            <td>{item.FechaHora:dd/MM/yyyy HH:mm:ss}</td>
            <td>
                <a href='/Lecturas/Details/{item.LecturaId}' class='btn-small waves-effect waves-light blue darken-1'>Detalles</a>{botonesAdmin}
            </td>
        </tr>";
    }));

    string body = $@"
<div class='row'>
    <div class='col s12'>
        <div class='card white z-depth-1'>
            <div class='card-content'>
                <div class='row' style='margin-bottom:0;'>
                    <div class='col s12 m8'>
                        <span class='card-title teal-text text-darken-3'>Historial de Lecturas de Temperatura</span>
                        <p class='grey-text text-darken-1'>Ordenado de la lectura más reciente a la más antigua.</p>
                    </div>
                    <div class='col s12 m4 right-align' style='margin-top:12px;'>
                        <a href='/Lecturas/Create' class='btn waves-effect waves-light teal darken-2'>Registrar Lectura Manual</a>
                    </div>
                </div>
                <table class='striped highlight responsive-table' style='margin-top:16px;'>
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Dispositivo</th>
                            <th>Puerto COM</th>
                            <th>Temperatura (°C)</th>
                            <th>Fecha y Hora</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>{filas}</tbody>
                </table>
            </div>
        </div>
    </div>
</div>";
    return Results.Content(PlantillaHtml.RenderLayout("Historial de Lecturas", ctx.User, body), "text/html; charset=utf-8");
});

app.MapGet("/Lecturas/Details/{id:int}", (HttpContext ctx, int id) =>
{
    if (!ctx.User.Identity.IsAuthenticated)
        return Results.Redirect("/Account/Login");

    var item = BaseDatosDocker.ObtenerLecturasOrdenadas().FirstOrDefault(l => l.LecturaId == id);
    if (item == null) return Results.NotFound();

    string btnEdit = ctx.User.IsInRole("Administrador")
        ? $"<a href='/Lecturas/Edit/{item.LecturaId}' class='btn amber darken-3 waves-effect waves-light'>Editar</a>"
        : "";

    string body = $@"
<div class='row'>
    <div class='col s12 m8 offset-m2'>
        <div class='card white z-depth-1'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Detalles de la Lectura #{item.LecturaId}</span>
                <ul class='collection'>
                    <li class='collection-item'><strong>Dispositivo:</strong> {WebUtility.HtmlEncode(item.NombreDispositivo)}</li>
                    <li class='collection-item'><strong>Puerto COM:</strong> {WebUtility.HtmlEncode(item.PuertoCOM)}</li>
                    <li class='collection-item'><strong>Temperatura:</strong> {item.Temperatura.ToString("0.00", CultureInfo.InvariantCulture)} °C</li>
                    <li class='collection-item'><strong>Fecha y hora:</strong> {item.FechaHora:dd/MM/yyyy HH:mm:ss}</li>
                </ul>
            </div>
            <div class='card-action'>
                {btnEdit}
                <a href='/Lecturas' class='btn-flat teal-text text-darken-3'>Volver al historial</a>
            </div>
        </div>
    </div>
</div>";
    return Results.Content(PlantillaHtml.RenderLayout("Detalles de Lectura", ctx.User, body), "text/html; charset=utf-8");
});

app.MapGet("/Lecturas/Create", (HttpContext ctx, string error = null) =>
{
    if (!ctx.User.Identity.IsAuthenticated)
        return Results.Redirect("/Account/Login");

    var dispositivos = BaseDatosDocker.ObtenerDispositivosActivos();
    string opciones = string.Join("", dispositivos.Select(d => $"<option value='{d.DispositivoId}'>{WebUtility.HtmlEncode(d.Nombre)}</option>"));
    string msgError = !string.IsNullOrWhiteSpace(error) ? $"<p class='field-validation-error'>{WebUtility.HtmlEncode(error)}</p>" : "";

    string body = $@"
<div class='row'>
    <div class='col s12 m8 offset-m2'>
        <div class='card white z-depth-1'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Registrar Nueva Lectura</span>
                <p class='grey-text text-darken-1'>El dispositivo y la temperatura son obligatorios. Rango permitido: −40 a 125 °C.</p>
                {msgError}
                <form method='post' action='/Lecturas/Create'>
                    <div class='section'>
                        <label class='black-text'>Dispositivo</label>
                        <select name='DispositivoId' class='browser-default' required>{opciones}</select>
                    </div>
                    <div class='input-field section'>
                        <input id='Temperatura' name='Temperatura' type='number' step='0.01' required />
                        <label for='Temperatura' class='active'>Temperatura (°C)</label>
                    </div>
                    <div class='section'>
                        <button type='submit' class='btn waves-effect waves-light teal darken-2'>Guardar Lectura</button>
                        <a href='/Lecturas' class='btn-flat'>Cancelar</a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>";
    return Results.Content(PlantillaHtml.RenderLayout("Registrar Nueva Lectura", ctx.User, body), "text/html; charset=utf-8");
});

app.MapPost("/Lecturas/Create", async (HttpContext ctx) =>
{
    if (!ctx.User.Identity.IsAuthenticated)
        return Results.Redirect("/Account/Login");

    var form = await ctx.Request.ReadFormAsync();
    if (!int.TryParse(form["DispositivoId"], out int dispositivoId) || dispositivoId <= 0)
        return Results.Redirect("/Lecturas/Create?error=" + Uri.EscapeDataString("El dispositivo es obligatorio."));

    string tempRaw = form["Temperatura"].ToString().Replace(",", ".");
    if (!decimal.TryParse(tempRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal temp))
        return Results.Redirect("/Lecturas/Create?error=" + Uri.EscapeDataString("La temperatura es obligatoria."));

    if (temp < -40m || temp > 125m)
        return Results.Redirect("/Lecturas/Create?error=" + Uri.EscapeDataString("La temperatura debe estar en el rango de -40 a 125 °C."));

    BaseDatosDocker.InsertarLectura(dispositivoId, Math.Round(temp, 2), DateTime.Now);
    return Results.Redirect("/Lecturas");
});

app.MapGet("/Lecturas/Edit/{id:int}", (HttpContext ctx, int id, string error = null) =>
{
    if (!ctx.User.Identity.IsAuthenticated || !ctx.User.IsInRole("Administrador"))
        return Results.Redirect("/Account/Login");

    var item = BaseDatosDocker.ObtenerLecturasOrdenadas().FirstOrDefault(l => l.LecturaId == id);
    if (item == null) return Results.NotFound();

    var dispositivos = BaseDatosDocker.ObtenerDispositivosActivos();
    string opciones = string.Join("", dispositivos.Select(d =>
        $"<option value='{d.DispositivoId}' {(d.DispositivoId == item.DispositivoId ? "selected" : "")}>{WebUtility.HtmlEncode(d.Nombre)}</option>"));
    string msgError = !string.IsNullOrWhiteSpace(error) ? $"<p class='field-validation-error'>{WebUtility.HtmlEncode(error)}</p>" : "";

    string body = $@"
<div class='row'>
    <div class='col s12 m8 offset-m2'>
        <div class='card white z-depth-1'>
            <div class='card-content'>
                <span class='card-title teal-text text-darken-3'>Editar Lectura #{item.LecturaId} (Solo Administrador)</span>
                {msgError}
                <form method='post' action='/Lecturas/Edit/{item.LecturaId}'>
                    <div class='section'>
                        <label class='black-text'>Dispositivo</label>
                        <select name='DispositivoId' class='browser-default' required>{opciones}</select>
                    </div>
                    <div class='input-field section'>
                        <input id='Temperatura' name='Temperatura' type='number' step='0.01' value='{item.Temperatura.ToString("0.00", CultureInfo.InvariantCulture)}' required />
                        <label for='Temperatura' class='active'>Temperatura (°C)</label>
                    </div>
                    <div class='section'>
                        <button type='submit' class='btn waves-effect waves-light amber darken-3'>Guardar Cambios</button>
                        <a href='/Lecturas' class='btn-flat'>Volver al historial</a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>";
    return Results.Content(PlantillaHtml.RenderLayout("Editar Lectura", ctx.User, body), "text/html; charset=utf-8");
});

app.MapPost("/Lecturas/Edit/{id:int}", async (HttpContext ctx, int id) =>
{
    if (!ctx.User.Identity.IsAuthenticated || !ctx.User.IsInRole("Administrador"))
        return Results.Redirect("/Account/Login");

    var form = await ctx.Request.ReadFormAsync();
    int.TryParse(form["DispositivoId"], out int dispositivoId);
    string tempRaw = form["Temperatura"].ToString().Replace(",", ".");
    if (!decimal.TryParse(tempRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal temp) || temp < -40m || temp > 125m)
    {
        return Results.Redirect($"/Lecturas/Edit/{id}?error=" + Uri.EscapeDataString("La temperatura debe estar en el rango de -40 a 125 °C."));
    }

    BaseDatosDocker.EditarLectura(id, dispositivoId, Math.Round(temp, 2));
    return Results.Redirect("/Lecturas");
});

app.MapGet("/Lecturas/Delete/{id:int}", (HttpContext ctx, int id) =>
{
    if (!ctx.User.Identity.IsAuthenticated || !ctx.User.IsInRole("Administrador"))
        return Results.Redirect("/Account/Login");

    var item = BaseDatosDocker.ObtenerLecturasOrdenadas().FirstOrDefault(l => l.LecturaId == id);
    if (item == null) return Results.NotFound();

    string body = $@"
<div class='row'>
    <div class='col s12 m8 offset-m2'>
        <div class='card white z-depth-1'>
            <div class='card-content'>
                <span class='card-title red-text text-darken-3'>Confirmar Eliminación de Lectura #{item.LecturaId}</span>
                <p>¿Está seguro de que desea eliminar este registro de lectura? Esta acción requiere el rol <strong>Administrador</strong>.</p>
                <ul class='collection' style='margin-top:16px;'>
                    <li class='collection-item'><strong>Dispositivo:</strong> {WebUtility.HtmlEncode(item.NombreDispositivo)}</li>
                    <li class='collection-item'><strong>Temperatura:</strong> {item.Temperatura.ToString("0.00", CultureInfo.InvariantCulture)} °C</li>
                    <li class='collection-item'><strong>Fecha y hora:</strong> {item.FechaHora:dd/MM/yyyy HH:mm:ss}</li>
                </ul>
                <form method='post' action='/Lecturas/Delete/{item.LecturaId}'>
                    <div class='section'>
                        <button type='submit' class='btn waves-effect waves-light red darken-2'>Confirmar Eliminación</button>
                        <a href='/Lecturas' class='btn-flat'>Cancelar</a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>";
    return Results.Content(PlantillaHtml.RenderLayout("Eliminar Lectura", ctx.User, body), "text/html; charset=utf-8");
});

app.MapPost("/Lecturas/Delete/{id:int}", (HttpContext ctx, int id) =>
{
    if (!ctx.User.Identity.IsAuthenticated || !ctx.User.IsInRole("Administrador"))
        return Results.Redirect("/Account/Login");

    BaseDatosDocker.EliminarLectura(id);
    return Results.Redirect("/Lecturas");
});

app.Run();

// ==================== CLASES DE APOYO Y BASE DE DATOS SQL SERVER ====================
public class DispositivoDto
{
    public int DispositivoId { get; set; }
    public string Nombre { get; set; }
    public string PuertoCOM { get; set; }
    public bool Activo { get; set; }
}

public class LecturaDto
{
    public int LecturaId { get; set; }
    public int DispositivoId { get; set; }
    public string NombreDispositivo { get; set; }
    public string PuertoCOM { get; set; }
    public decimal Temperatura { get; set; }
    public DateTime FechaHora { get; set; }
}

public class UsuarioDto
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string Rol { get; set; }
}

public static class PlantillaHtml
{
    public static string RenderLayout(string titulo, ClaimsPrincipal user, string bodyHtml)
    {
        bool autenticado = user?.Identity?.IsAuthenticated ?? false;
        string nombreUsuario = autenticado ? user.Identity.Name : "";
        bool esAdmin = autenticado && user.IsInRole("Administrador");

        string loginPartial = autenticado
            ? $@"<li>
                    <span class='white-text' style='padding: 0 12px;'>
                        Usuario: <strong>{WebUtility.HtmlEncode(nombreUsuario)}</strong>
                        {(esAdmin ? "<span class='badge-rol amber darken-2 black-text'>Administrador</span>" : "")}
                    </span>
                 </li>
                 <li>
                    <form id='logoutForm' method='post' action='/Account/LogOff' style='display:inline;'>
                        <a href=""javascript:document.getElementById('logoutForm').submit()"" class='waves-effect waves-light btn-small red darken-2'>Cerrar sesión</a>
                    </form>
                 </li>"
            : @"<li><a href='/Account/Register'>Registrarse</a></li>
                <li><a href='/Account/Login'>Iniciar sesión</a></li>";

        return $@"<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>{WebUtility.HtmlEncode(titulo)} - MonitorSerial</title>
    <link rel='stylesheet' href='/Content/materialize.min.css' />
    <link rel='stylesheet' href='/Content/Site.css' />
</head>
<body>
    <nav class='teal darken-3' role='navigation'>
        <div class='nav-wrapper container'>
            <a href='/Sensor' class='brand-logo'>MonitorSerial</a>
            <ul class='right hide-on-med-and-down'>
                <li><a href='/Sensor'>Monitor de Temperatura</a></li>
                <li><a href='/Lecturas'>Historial de Lecturas</a></li>
                {loginPartial}
            </ul>
        </div>
    </nav>
    <main class='container'>{bodyHtml}</main>
    <footer class='page-footer teal darken-4'>
        <div class='footer-copyright'>
            <div class='container'>
                &copy; {DateTime.Now.Year} - MonitorSerial | Vigilancia de Temperatura en Sala de Equipos
            </div>
        </div>
    </footer>
    <script src='/Scripts/jquery-3.7.1.min.js'></script>
    <script src='/Scripts/materialize.min.js'></script>
    <script>
        document.addEventListener('DOMContentLoaded', function () {{
            if (typeof M !== 'undefined' && M.AutoInit) {{
                M.AutoInit();
            }}
        }});
    </script>
</body>
</html>";
    }
}

public static class BaseDatosDocker
{
    private static readonly object _lock = new object();
    private static bool _sqlListo = false;

    private static readonly List<DispositivoDto> _dispositivosMem = new()
    {
        new DispositivoDto { DispositivoId = 1, Nombre = "Sensor Sala de Equipos Principal", PuertoCOM = "COM3", Activo = true },
        new DispositivoDto { DispositivoId = 2, Nombre = "Sensor Rack Respaldo", PuertoCOM = "COM1", Activo = true }
    };

    private static readonly List<LecturaDto> _lecturasMem = new()
    {
        new LecturaDto { LecturaId = 1, DispositivoId = 1, NombreDispositivo = "Sensor Sala de Equipos Principal", PuertoCOM = "COM3", Temperatura = 22.50m, FechaHora = DateTime.Now.AddMinutes(-30) },
        new LecturaDto { LecturaId = 2, DispositivoId = 1, NombreDispositivo = "Sensor Sala de Equipos Principal", PuertoCOM = "COM3", Temperatura = 23.10m, FechaHora = DateTime.Now.AddMinutes(-15) },
        new LecturaDto { LecturaId = 3, DispositivoId = 1, NombreDispositivo = "Sensor Sala de Equipos Principal", PuertoCOM = "COM3", Temperatura = 21.85m, FechaHora = DateTime.Now }
    };

    private static readonly List<UsuarioDto> _usuarios = new()
    {
        new UsuarioDto { Email = "admin@monitorserial.local", Password = "DemoLab2026", Rol = "Administrador" },
        new UsuarioDto { Email = "operador@monitorserial.local", Password = "DemoLab2026", Rol = "Operador" }
    };

    private static string ConnMaster => Environment.GetEnvironmentVariable("MASTER_CONNECTION_STRING");
    private static string ConnDb => Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");

    public static async Task InicializarSqlConReintentosAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnMaster) || string.IsNullOrWhiteSpace(ConnDb)) return;

        for (int intento = 1; intento <= 20; intento++)
        {
            try
            {
                using (var conn = new SqlConnection(ConnMaster))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
IF DB_ID(N'MonitorSerial') IS NULL
BEGIN
    CREATE DATABASE [MonitorSerial];
END";
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var conn = new SqlConnection(ConnDb))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
IF OBJECT_ID(N'dbo.Dispositivo', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Dispositivo] (
        [DispositivoId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Nombre]        NVARCHAR(100) NOT NULL,
        [PuertoCOM]     NVARCHAR(20)  NOT NULL,
        [Activo]        BIT           NOT NULL DEFAULT (1)
    );
    CREATE TABLE [dbo].[Lectura] (
        [LecturaId]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DispositivoId] INT           NOT NULL,
        [Temperatura]   DECIMAL(5,2)  NOT NULL,
        [FechaHora]     DATETIME      NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [FK_Lectura_Dispositivo] FOREIGN KEY ([DispositivoId])
            REFERENCES [dbo].[Dispositivo]([DispositivoId]) ON DELETE CASCADE
    );
    INSERT INTO [dbo].[Dispositivo] ([Nombre], [PuertoCOM], [Activo])
    VALUES (N'Sensor Sala de Equipos Principal', N'COM3', 1),
           (N'Sensor Rack Respaldo', N'COM1', 1);
    INSERT INTO [dbo].[Lectura] ([DispositivoId], [Temperatura], [FechaHora])
    VALUES (1, 22.50, DATEADD(MINUTE, -30, GETDATE())),
           (1, 23.10, DATEADD(MINUTE, -15, GETDATE())),
           (1, 21.85, GETDATE());
END";
                    await cmd.ExecuteNonQueryAsync();
                }

                _sqlListo = true;
                Console.WriteLine("[MonitorSerial] Base de datos SQL Server inicializada correctamente.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitorSerial] Esperando contenedor SQL Server (intento {intento}/20): {ex.Message}");
                await Task.Delay(3000);
            }
        }
    }

    public static UsuarioDto ValidarUsuario(string email, string password)
    {
        lock (_lock)
        {
            return _usuarios.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) &&
                u.Password == password);
        }
    }

    public static UsuarioDto RegistrarUsuario(string email, string password)
    {
        lock (_lock)
        {
            var existente = _usuarios.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            if (existente != null) return existente;
            var nuevo = new UsuarioDto { Email = email, Password = password, Rol = "Operador" };
            _usuarios.Add(nuevo);
            return nuevo;
        }
    }

    public static List<DispositivoDto> ObtenerDispositivosActivos()
    {
        if (_sqlListo)
        {
            try
            {
                var lista = new List<DispositivoDto>();
                using var conn = new SqlConnection(ConnDb);
                conn.Open();
                using var cmd = new SqlCommand("SELECT DispositivoId, Nombre, PuertoCOM, Activo FROM dbo.Dispositivo WHERE Activo = 1 ORDER BY Nombre", conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    lista.Add(new DispositivoDto
                    {
                        DispositivoId = rdr.GetInt32(0),
                        Nombre = rdr.GetString(1),
                        PuertoCOM = rdr.GetString(2),
                        Activo = rdr.GetBoolean(3)
                    });
                }
                return lista;
            }
            catch { }
        }
        lock (_lock) return _dispositivosMem.Where(d => d.Activo).ToList();
    }

    public static List<LecturaDto> ObtenerLecturasOrdenadas()
    {
        if (_sqlListo)
        {
            try
            {
                var lista = new List<LecturaDto>();
                using var conn = new SqlConnection(ConnDb);
                conn.Open();
                using var cmd = new SqlCommand(@"
SELECT l.LecturaId, l.DispositivoId, d.Nombre, d.PuertoCOM, l.Temperatura, l.FechaHora
FROM dbo.Lectura l
INNER JOIN dbo.Dispositivo d ON l.DispositivoId = d.DispositivoId
ORDER BY l.FechaHora DESC", conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    lista.Add(new LecturaDto
                    {
                        LecturaId = rdr.GetInt32(0),
                        DispositivoId = rdr.GetInt32(1),
                        NombreDispositivo = rdr.GetString(2),
                        PuertoCOM = rdr.GetString(3),
                        Temperatura = rdr.GetDecimal(4),
                        FechaHora = rdr.GetDateTime(5)
                    });
                }
                return lista;
            }
            catch { }
        }
        lock (_lock) return _lecturasMem.OrderByDescending(l => l.FechaHora).ToList();
    }

    public static LecturaDto InsertarLectura(int dispositivoId, decimal temperatura, DateTime fechaHora)
    {
        if (_sqlListo)
        {
            try
            {
                using var conn = new SqlConnection(ConnDb);
                conn.Open();
                using var cmd = new SqlCommand(@"
INSERT INTO dbo.Lectura (DispositivoId, Temperatura, FechaHora)
OUTPUT INSERTED.LecturaId
VALUES (@disp, @temp, @fecha)", conn);
                cmd.Parameters.AddWithValue("@disp", dispositivoId);
                cmd.Parameters.AddWithValue("@temp", temperatura);
                cmd.Parameters.AddWithValue("@fecha", fechaHora);
                int id = (int)cmd.ExecuteScalar();
                return new LecturaDto { LecturaId = id, DispositivoId = dispositivoId, Temperatura = temperatura, FechaHora = fechaHora };
            }
            catch { }
        }
        lock (_lock)
        {
            var disp = _dispositivosMem.FirstOrDefault(d => d.DispositivoId == dispositivoId) ?? _dispositivosMem.First();
            int id = _lecturasMem.Count > 0 ? _lecturasMem.Max(l => l.LecturaId) + 1 : 1;
            var nueva = new LecturaDto
            {
                LecturaId = id,
                DispositivoId = disp.DispositivoId,
                NombreDispositivo = disp.Nombre,
                PuertoCOM = disp.PuertoCOM,
                Temperatura = temperatura,
                FechaHora = fechaHora
            };
            _lecturasMem.Add(nueva);
            return nueva;
        }
    }

    public static void EditarLectura(int lecturaId, int dispositivoId, decimal temperatura)
    {
        if (_sqlListo)
        {
            try
            {
                using var conn = new SqlConnection(ConnDb);
                conn.Open();
                using var cmd = new SqlCommand("UPDATE dbo.Lectura SET DispositivoId = @disp, Temperatura = @temp WHERE LecturaId = @id", conn);
                cmd.Parameters.AddWithValue("@disp", dispositivoId);
                cmd.Parameters.AddWithValue("@temp", temperatura);
                cmd.Parameters.AddWithValue("@id", lecturaId);
                cmd.ExecuteNonQuery();
                return;
            }
            catch { }
        }
        lock (_lock)
        {
            var item = _lecturasMem.FirstOrDefault(l => l.LecturaId == lecturaId);
            var disp = _dispositivosMem.FirstOrDefault(d => d.DispositivoId == dispositivoId);
            if (item != null && disp != null)
            {
                item.DispositivoId = disp.DispositivoId;
                item.NombreDispositivo = disp.Nombre;
                item.PuertoCOM = disp.PuertoCOM;
                item.Temperatura = temperatura;
            }
        }
    }

    public static void EliminarLectura(int lecturaId)
    {
        if (_sqlListo)
        {
            try
            {
                using var conn = new SqlConnection(ConnDb);
                conn.Open();
                using var cmd = new SqlCommand("DELETE FROM dbo.Lectura WHERE LecturaId = @id", conn);
                cmd.Parameters.AddWithValue("@id", lecturaId);
                cmd.ExecuteNonQuery();
                return;
            }
            catch { }
        }
        lock (_lock)
        {
            _lecturasMem.RemoveAll(l => l.LecturaId == lecturaId);
        }
    }
}
