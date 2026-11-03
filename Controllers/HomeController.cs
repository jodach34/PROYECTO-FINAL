using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Punto de entrada publico de Rescauta. La portada resumen la red y muestra el mapa;
/// las paginas institucionales comparten el mismo layout y el mismo lenguaje visual.
/// </summary>
public class HomeController(RescautaApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var resultado = await api.ObtenerComedoresAsync(cancellationToken);

        ViewData["Title"] = "Comida que vuelve a servir";
        ViewData["MetaDescription"] = "Rescauta conecta alimentos disponibles con comedores comunitarios para reducir el desperdicio y responder a tiempo.";

        return View(new MapaViewModel
        {
            Comedores = resultado.Exito ? resultado.Valor! : [],
            Error = resultado.Exito ? null : resultado.Mensaje
        });
    }

    [Route("Nosotros")]
    public IActionResult Nosotros()
    {
        ViewData["Title"] = "Nosotros";
        ViewData["MetaDescription"] = "Conoce el propósito, los principios y el equipo detrás de Rescauta.";
        return View();
    }

    [Route("ComoFunciona")]
    public IActionResult ComoFunciona()
    {
        ViewData["Title"] = "Cómo funciona";
        ViewData["MetaDescription"] = "Del alimento disponible al plato servido: así funciona la red Rescauta.";
        return View();
    }

    [Route("Donaciones")]
    public IActionResult Donaciones()
    {
        ViewData["Title"] = "Donaciones";
        ViewData["MetaDescription"] = "Dona alimentos, tiempo o recursos y ayuda a sostener los comedores comunitarios.";
        return View();
    }

    [Route("Contacto")]
    public IActionResult Contacto()
    {
        ViewData["Title"] = "Contacto";
        ViewData["MetaDescription"] = "Escríbenos si representas un comedor, quieres donar o deseas sumarte como voluntario.";
        return View();
    }

    [Route("Privacidad")]
    public IActionResult Privacidad()
    {
        ViewData["Title"] = "Privacidad";
        ViewData["MetaDescription"] = "Cómo trata Rescauta los datos de comedores, donantes y visitantes.";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
