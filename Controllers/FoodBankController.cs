using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Banco de Alimentos: registra una donacion contra un insumo concreto de un comedor.
///
/// El POST usa PRG (Post/Redirect/Get): al guardar, redirige en vez de repintar. Asi el
/// F5 del operador no vuelve a mandar la misma donacion, que es el fallo clasico de un
/// formulario que se queda en la misma pantalla tras guardar.
/// </summary>
public class FoodBankController(RescautaApiClient api) : Controller
{
    /// <param name="registrado">
    /// Codigo de seguimiento que viaja desde el POST tras un registro exitoso (PRG). Se
    /// muestra una vez y ya no se queda pegado en la URL al recargar.
    /// </param>
    public async Task<IActionResult> Index(string? registrado, CancellationToken cancellationToken)
    {
        var resultado = await CargarAsync(cancellationToken);

        return View(resultado with { Exito = registrado });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(DonacionViewModel formulario, CancellationToken cancellationToken)
    {
        // Se validan aqui y no solo con DataAnnotations porque los mensajes de ModelState son
        // genericos y el operador necesita saber que campo esta mal.
        var errores = new List<string>();

        if (formulario.InsumoId is null || formulario.InsumoId == Guid.Empty)
        {
            errores.Add("Elige el insumo que se va a donar.");
        }

        if (formulario.Cantidad <= 0)
        {
            errores.Add("La cantidad debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(formulario.Donante))
        {
            errores.Add("Escribe el nombre de quien dona.");
        }

        if (errores.Count > 0)
        {
            var conErrores = await CargarAsync(cancellationToken);

            return View("Index", conErrores with
            {
                Error = string.Join(" ", errores),
                Donante = formulario.Donante,
                Cantidad = formulario.Cantidad,
                PuntoRecojo = formulario.PuntoRecojo,
                InsumoId = formulario.InsumoId
            });
        }

        var donacion = await api.RegistrarDonacionAsync(
            new DonacionRequest
            {
                InsumoId = formulario.InsumoId!.Value,
                Cantidad = formulario.Cantidad,
                Donante = formulario.Donante.Trim(),
                PuntoRecojo = formulario.PuntoRecojo ?? string.Empty
            },
            cancellationToken);

        if (!donacion.Exito)
        {
            var conError = await CargarAsync(cancellationToken);

            return View("Index", conError with
            {
                Error = donacion.Mensaje,
                Donante = formulario.Donante,
                Cantidad = formulario.Cantidad,
                PuntoRecojo = formulario.PuntoRecojo,
                InsumoId = formulario.InsumoId
            });
        }

        // PRG: el codigo de seguimiento viaja en el query string para que la vista pueda
        // mostrarlo una sola vez, y el F5 ya no vuelve a enviar el formulario.
        return RedirectToAction(
            nameof(Index),
            new { registrado = donacion.Valor!.CodigoSeguimiento });
    }

    /// <summary>
    /// Carga lo que la pantalla necesita para pintarse. Cada parte trae su propio error: si
    /// la lista de donaciones falla pero los comedores no, la pantalla se dibuja igual y el
    /// aviso va donde corresponde.
    /// </summary>
    private async Task<BancoAlimentosViewModel> CargarAsync(CancellationToken cancellationToken)
    {
        var resumen = await api.ObtenerComedoresAsync(cancellationToken);

        if (!resumen.Exito)
        {
            return new BancoAlimentosViewModel { Error = resumen.Mensaje };
        }

        // El detalle de cada comedor, en paralelo: sin el inventario no hay selector de
        // insumos que llenarse, asi que esta llamada es la que mas pesa de la pantalla.
        var detalles = await Task.WhenAll(
            resumen.Valor!.Select(c => api.ObtenerComedorAsync(c.Id, cancellationToken)));

        var comedores = detalles
            .Where(d => d.Exito)
            .Select(d => d.Valor!)
            .OrderBy(c => c.Nombre)
            .ToList();

        var donaciones = await api.ObtenerDonacionesAsync(10, cancellationToken);

        return new BancoAlimentosViewModel
        {
            Comedores = comedores,
            Donaciones = donaciones.Exito ? donaciones.Valor! : [],
            Error = resumen.Exito ? (donaciones.Exito ? null : donaciones.Mensaje) : resumen.Mensaje
        };
    }
}
