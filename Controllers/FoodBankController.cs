using Microsoft.AspNetCore.Mvc;

namespace RescautaApp.Controllers
{
    public class FoodBankController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}