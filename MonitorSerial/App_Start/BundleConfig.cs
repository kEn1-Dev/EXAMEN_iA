using System.Web.Optimization;

namespace MonitorSerial
{
    public class BundleConfig
    {
        // Parte 1.2: Integración local de Materialize en Content y Scripts (sin CDN y sin Bootstrap)
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-3.7.1.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate.min.js",
                        "~/Scripts/jquery.validate.unobtrusive.min.js"));

            // Registro local de Materialize JS (sin referencias a Bootstrap)
            bundles.Add(new ScriptBundle("~/bundles/materialize").Include(
                        "~/Scripts/materialize.min.js"));

            // Registro local de Materialize CSS y estilos del sitio (sin referencias a Bootstrap)
            bundles.Add(new StyleBundle("~/Content/css").Include(
                        "~/Content/materialize.min.css",
                        "~/Content/Site.css"));
        }
    }
}
