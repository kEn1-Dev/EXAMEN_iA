using System.Web.Mvc;
using System.Web.Routing;

namespace MonitorSerial
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // La vista principal del sistema apunta a SensorController.Index (Parte 3.4)
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Sensor", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
