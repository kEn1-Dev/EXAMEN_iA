using System.Web.Mvc;

namespace MonitorSerial.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Sensor");
        }

        public ActionResult Error()
        {
            return View("Error");
        }
    }
}
