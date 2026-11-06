using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Gestion administrativa de donaciones: listado filtrado, detalle, linea de tiempo y
/// acciones de estado. El registro publico sigue viviendo en FoodBankController.
///
/// El controller no decide permisos finos por comedor: eso lo hace la API con los claims del
/// token. Aqui solo se traduce el resultado a TempData y se redirige.
/// </summary>
[Authorize(Roles = "Operador,Coordinador,Administrador")]
public class DonacionesController(RescautaApiClient api) : Controller
{
    public async Task<IActionResult> Index(
        string? estado,
        Guid? comedorId,
        string? busqueda,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        if (tamanoPagina is < 1 or > 100)
        {
            tamanoPagina = 20;
        }

        var filtro = new DonacionFiltroRequest
        {
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado,
            ComedorId = comedorId,
            Busqueda = busqueda,
            Pagina = pagina,
            TamanoPagina = tamanoPagina
        };

        var donacionesTask = api.BuscarDonacionesAsync(filtro, cancellationToken);
        var comedoresTask = api.ObtenerComedoresAsync(cancellationToken);

        await Task.WhenAll(donacionesTask, comedoresTask);

        var donaciones = await donacionesTask;
        var comedores = await comedoresTask;

        var modelo = new DonacionesGestionViewModel
        {
            Donaciones = donaciones.Exito ? donaciones.Valor!.Items : [],
            Comedores = comedores.Exito ? comedores.Valor! : [],
            Estado = filtro.Estado,
            ComedorId = comedorId,
            Busqueda = busqueda,
            Pagina = donaciones.Exito ? donaciones.Valor!.PageNumber : pagina,
            TamanoPagina = tamanoPagina,
            TotalCount = donaciones.Exito ? donaciones.Valor!.TotalCount : 0,
            TotalPages = donaciones.Exito ? donaciones.Valor!.TotalPages : 0,
            HasPrevious = donaciones.Exito && donaciones.Valor!.HasPrevious,
            HasNext = donaciones.Exito && donaciones.Valor!.HasNext,
            Error = donaciones.Exito ? null : donaciones.Mensaje,
            Exito = TempData["Exito"] as string
        };

        if (modelo.Error is null && !comedores.Exito)
        {
            modelo = modelo with { Error = comedores.Mensaje };
        }

        return View(modelo);
    }

    public async Task<IActionResult> Detalle(Guid id, CancellationToken cancellationToken)
    {
        var donacionTask = api.ObtenerDonacionAsync(id, cancellationToken);
        var historialTask = api.ObtenerHistorialDonacionAsync(id, cancellationToken);

        await Task.WhenAll(donacionTask, historialTask);

        var donacion = await donacionTask;
        var historial = await historialTask;

        var modelo = new DonacionDetalleViewModel
        {
            Donacion = donacion.Exito ? donacion.Valor : null,
            Historial = historial.Exito ? historial.Valor! : [],
            Error = donacion.Exito ? null : donacion.Mensaje,
            Exito = TempData["Exito"] as string
        };

        if (modelo.Error is null && !historial.Exito)
        {
            modelo = modelo with { Error = historial.Mensaje };
        }

        if (modelo.Donacion is null && modelo.Error is null)
        {
            modelo = modelo with { Error = "No se encontro la donacion solicitada." };
        }

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await api.ConfirmarDonacionAsync(id, cancellationToken);

        return VolverAlDetalle(
            id,
            resultado,
            "Recepcion confirmada. El stock ya ingreso al inventario.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnTransito(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await api.MarcarDonacionEnTransitoAsync(id, cancellationToken);

        return VolverAlDetalle(id, resultado, "La donacion quedo marcada en transito.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entregar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await api.EntregarDonacionAsync(id, cancellationToken);

        return VolverAlDetalle(id, resultado, "La donacion quedo entregada.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(
        Guid id,
        string motivo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "Indica el motivo de la cancelacion.";

            return RedirectToAction(nameof(Detalle), new { id });
        }

        var resultado = await api.CancelarDonacionAsync(id, motivo, cancellationToken);

        return VolverAlDetalle(id, resultado, "La donacion quedo cancelada.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(
        Guid id,
        string motivo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "Indica el motivo de la anulacion.";

            return RedirectToAction(nameof(Detalle), new { id });
        }

        var resultado = await api.AnularDonacionAsync(id, motivo, cancellationToken);

        return VolverAlDetalle(id, resultado, "La donacion quedo anulada.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await api.EliminarDonacionAsync(id, cancellationToken);

        if (resultado.Exito)
        {
            TempData["Exito"] = "La donacion quedo eliminada del listado.";

            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = resultado.Mensaje;

        return RedirectToAction(nameof(Detalle), new { id });
    }

    private IActionResult VolverAlDetalle(
        Guid id,
        Result<DonacionRegistrada> resultado,
        string mensajeExito)
    {
        TempData[resultado.Exito ? "Exito" : "Error"] =
            resultado.Exito ? mensajeExito : resultado.Mensaje;

        return RedirectToAction(nameof(Detalle), new { id });
    }
}
