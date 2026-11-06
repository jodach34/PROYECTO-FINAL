using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Directorio publico de comedores y ficha de detalle. La API sigue siendo la unica
/// fuente de verdad: si no responde, la pagina se dibuja con un aviso claro.
/// </summary>
public class ComedoresController(RescautaApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var mapa = await MapaComedoresLoader.CargarAsync(api, cancellationToken);

        ViewData["Title"] = "Directorio de comedores";
        ViewData["MetaDescription"] = "Explora los comedores comunitarios de la red, su nivel de abastecimiento y las raciones que preparan cada día.";
        ViewData["UsaMapa"] = true;

        return View(mapa);
    }

    public async Task<IActionResult> Detalle(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await api.ObtenerComedorAsync(id, cancellationToken);

        ViewData["Title"] = resultado.Exito ? resultado.Valor!.Nombre : "Comedor no encontrado";
        ViewData["MetaDescription"] = "Ficha del comedor comunitario y su inventario actual en la red Rescauta.";

        return View(new ComedorDetalleViewModel
        {
            Comedor = resultado.Exito ? resultado.Valor : null,
            Error = resultado.Exito ? null : resultado.Mensaje
        });
    }
}
