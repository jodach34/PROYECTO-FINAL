using Microsoft.AspNetCore.Mvc;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Maquetado estatico del Banco de Alimentos (registro de donacion).
/// Los comedores recomendados y el codigo de seguimiento son ficticios.
/// </summary>
public class FoodBankController : Controller
{
    public IActionResult Index() => View();
}
