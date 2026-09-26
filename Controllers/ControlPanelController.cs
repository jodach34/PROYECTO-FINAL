using Microsoft.AspNetCore.Mvc;

namespace RescautaApp.Controllers
{
    public class ControlPanelController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}