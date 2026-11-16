using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using MonitorSerial.Models;
using MonitorSerial.Services;
using Owin;

namespace MonitorSerial
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            app.CreatePerOwinContext(ApplicationDbContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login")
            });

            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            if (RepositorioRespaldoLocal.HaySqlServerDisponible())
            {
                InicializarRolesYUsuariosDePrueba();
            }
            else
            {
                // Inicializa el archivo local de respaldo con los datos de script.sql y los 2 usuarios de prueba
                RepositorioRespaldoLocal.CargarEstado();
            }
        }

        /// <summary>
        /// Parte 5.3 y 5.4: Crea el rol Administrador y registra dos usuarios de prueba
        /// (uno con rol Administrador y otro con perfil estándar de consulta/registro)
        /// utilizando credenciales ficticias exclusivas de laboratorio.
        /// </summary>
        private void InicializarRolesYUsuariosDePrueba()
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));
                    var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));

                    // 1. Crear el rol Administrador si no existe
                    const string rolAdmin = "Administrador";
                    if (!roleManager.RoleExists(rolAdmin))
                    {
                        roleManager.Create(new IdentityRole(rolAdmin));
                    }

                    // Clave ficticia de laboratorio (NO es una contraseña de uso real)
                    const string claveFicticiaLaboratorio = "DemoLab2026";

                    // 2. Usuario de prueba 1: Perfil Administrador
                    const string emailAdmin = "admin@monitorserial.local";
                    var usuarioAdmin = userManager.FindByEmail(emailAdmin);
                    if (usuarioAdmin == null)
                    {
                        usuarioAdmin = new ApplicationUser
                        {
                            UserName = emailAdmin,
                            Email = emailAdmin
                        };
                        var resultado = userManager.Create(usuarioAdmin, claveFicticiaLaboratorio);
                        if (resultado.Succeeded)
                        {
                            userManager.AddToRole(usuarioAdmin.Id, rolAdmin);
                        }
                    }

                    // 3. Usuario de prueba 2: Perfil Estándar (solo consulta y registra lecturas)
                    const string emailOperador = "operador@monitorserial.local";
                    var usuarioOperador = userManager.FindByEmail(emailOperador);
                    if (usuarioOperador == null)
                    {
                        usuarioOperador = new ApplicationUser
                        {
                            UserName = emailOperador,
                            Email = emailOperador
                        };
                        userManager.Create(usuarioOperador, claveFicticiaLaboratorio);
                    }
                }
            }
            catch
            {
                // Si la base de datos aún no está disponible al arrancar, se omite silenciosamente.
            }
        }
    }
}
