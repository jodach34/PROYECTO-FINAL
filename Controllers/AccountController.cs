using Microsoft.AspNetCore.Mvc;
using RescautaApp.Models;

namespace RescautaApp.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            // Validamos la regla solicitada: Clave '1234' y que ingrese un nombre
            if (model.Clave == "1234" && !string.IsNullOrEmpty(model.Usuario))
            {
                // Si es correcto, nos lleva a la página inicial que definimos como el Mapa (Home/Index)
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Credenciales incorrectas. Recuerda usar la clave: 1234");
            return View(model);
        }
    }
}