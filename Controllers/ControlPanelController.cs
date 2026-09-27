using Microsoft.AspNetCore.Mvc;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Maquetado estatico del Panel de Inventario y Kardex.
/// Los datos de las tarjetas y de la tabla son ficticios y viven en la vista.
/// </summary>
public class ControlPanelController : Controller
{
    public IActionResult Index() => View();
}
