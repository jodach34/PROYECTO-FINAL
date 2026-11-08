using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Seguimiento publico de una donacion por codigo. No lista donaciones ni expone ids
/// internos: el donante solo puede ver la donacion cuyo codigo conoce.
/// </summary>
public class SeguimientoController(RescautaApiClient api) : Controller
{
    public async Task<IActionResult> Index(string? codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return View(new SeguimientoViewModel());
        }

        // El codigo se genera en mayusculas. Se normaliza para que el donante pueda
        // escribirlo en minusculas o con espacios sin recibir un "no existe".
        var normalizado = codigo.Trim().ToUpperInvariant();

        var resultado = await api.ObtenerSeguimientoDonacionAsync(normalizado, cancellationToken);

        return View(new SeguimientoViewModel
        {
            Codigo = normalizado,
            Donacion = resultado.Exito ? resultado.Valor : null,
            Error = resultado.Exito ? null : resultado.Mensaje
        });
    }

}
