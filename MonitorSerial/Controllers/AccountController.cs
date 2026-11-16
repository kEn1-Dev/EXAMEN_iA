using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using MonitorSerial.Models;
using MonitorSerial.Services;

namespace MonitorSerial.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);
                switch (result)
                {
                    case SignInStatus.Success:
                        return RedirectToLocal(returnUrl);
                    case SignInStatus.LockedOut:
                        ModelState.AddModelError("", "La cuenta se encuentra bloqueada temporalmente.");
                        return View(model);
                    case SignInStatus.Failure:
                    default:
                        ModelState.AddModelError("", "Intento de inicio de sesión no válido. Verifique su correo y contraseña.");
                        return View(model);
                }
            }
            else
            {
                // Autenticación con el almacén local de respaldo cuando SQL Server no está instalado
                var estado = RepositorioRespaldoLocal.CargarEstado();
                var usuario = estado.Usuarios.FirstOrDefault(u =>
                    string.Equals(u.Email, model.Email != null ? model.Email.Trim() : "", StringComparison.OrdinalIgnoreCase) &&
                    u.Password == model.Password);

                if (usuario != null)
                {
                    EmitirCookieOwinLocal(usuario.Id, usuario.Email, usuario.Rol, model.RememberMe);
                    return RedirectToLocal(returnUrl);
                }

                ModelState.AddModelError("", "Intento de inicio de sesión no válido. Verifique su correo y contraseña.");
                return View(model);
            }
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (RepositorioRespaldoLocal.HaySqlServerDisponible())
                {
                    var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                    var result = await UserManager.CreateAsync(user, model.Password);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToAction("Index", "Sensor");
                    }
                    AddErrors(result);
                }
                else
                {
                    var estado = RepositorioRespaldoLocal.CargarEstado();
                    string emailLimpio = model.Email.Trim();
                    if (estado.Usuarios.Any(u => string.Equals(u.Email, emailLimpio, StringComparison.OrdinalIgnoreCase)))
                    {
                        ModelState.AddModelError("", "Ya existe un usuario registrado con ese correo electrónico.");
                        return View(model);
                    }

                    var nuevoUsuario = new RepositorioRespaldoLocal.UsuarioLocal
                    {
                        Id = "usr-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        Email = emailLimpio,
                        Password = model.Password,
                        Rol = "Operador"
                    };
                    estado.Usuarios.Add(nuevoUsuario);
                    RepositorioRespaldoLocal.GuardarEstado(estado);

                    EmitirCookieOwinLocal(nuevoUsuario.Id, nuevoUsuario.Email, nuevoUsuario.Rol, false);
                    return RedirectToAction("Index", "Sensor");
                }
            }

            return View(model);
        }

        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login", "Account");
        }

        private void EmitirCookieOwinLocal(string userId, string email, string rol, bool persistente)
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email),
                new Claim("http://schemas.microsoft.com/accesscontrolservice/2010/07/claims/identityprovider", "ASP.NET Identity"),
                new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(rol) ? "Operador" : rol)
            };

            var identity = new ClaimsIdentity(claims, DefaultAuthenticationTypes.ApplicationCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties { IsPersistent = persistente }, identity);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Sensor");
        }
        #endregion
    }
}
