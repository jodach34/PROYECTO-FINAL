using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Mapa Interactivo. Lee los comedores y su estado de abastecimiento de la API.
///
/// El controller no decide que hacer si la API falla: se lo pasa al ViewModel y la vista
/// avisa. Un mapa en blanco sin explicacion hace pensar que no hay comedores registrados,
/// que es distinto de "no pude preguntar".
/// </summary>
public class HomeController(RescautaApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var resultado = await api.ObtenerComedoresAsync(cancellationToken);

        return View(new MapaViewModel
        {
            Comedores = resultado.Exito ? resultado.Valor! : [],
            Error = resultado.Exito ? null : resultado.Mensaje
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
