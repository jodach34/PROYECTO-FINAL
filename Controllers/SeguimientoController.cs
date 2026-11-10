using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Seguimiento publico de una donacion por codigo. No lista donaciones ni expone ids
/// internos: el donante solo puede ver la donacion cuyo codigo conoce.
/// </summary>
public class SeguimientoController(
    RescautaApiClient api,
    RutaDonacionService rutas) : Controller
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

        var resultado = await api.ObtenerDonacionPorCodigoAsync(normalizado, cancellationToken);

        return View(new SeguimientoViewModel
        {
            Codigo = normalizado,
            Donacion = resultado.Exito ? resultado.Valor : null,
            Error = resultado.Exito ? null : resultado.Mensaje
        });
    }

    /// <summary>
    /// Ruta por carretera de la donacion consultada por su codigo. Es publica igual que el
    /// seguimiento: quien conoce el codigo ya ve el detalle, y este endpoint solo agrega el
    /// trazado del transporte. Si el origen no se marco, lo dice sin inventar coordenadas.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ruta(string codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Json(new RutaDonacionRespuesta
            {
                Motivo = "sin_codigo",
                Mensaje = "No se indico el codigo de la donacion."
            });
        }

        var donacion = await api.ObtenerRutaDonacionPorCodigoAsync(
            codigo.Trim().ToUpperInvariant(),
            cancellationToken);

        if (!donacion.Exito || donacion.Valor is null)
        {
            return Json(new RutaDonacionRespuesta
            {
                Motivo = donacion.Codigo ?? "api_no_disponible",
                Mensaje = donacion.Mensaje ?? "No se pudo leer la donacion."
            });
        }

        var paraRuta = new DonacionRegistrada
        {
            ComedorId = donacion.Valor.ComedorId,
            LatitudOrigen = donacion.Valor.LatitudOrigen,
            LongitudOrigen = donacion.Valor.LongitudOrigen
        };

        return Json(await rutas.CalcularAsync(paraRuta, cancellationToken));
    }
}
