using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Reportes del panel de administracion.
///
/// Los indicadores (total de donaciones recibidas y stock por insumo) salen de los
/// endpoints de reportes de la API, que ya resuelven el alcance del usuario: el
/// Administrador ve toda la red y Operador y Coordinador solo sus comedores asignados.
/// El detalle de donaciones reusa el listado de gestion, el unico endpoint que soporta
/// filtro por comedor y por rango de fechas; la API intersecta el comedor elegido con
/// los asignados al usuario, asi que aca no hace falta revalidar ese permiso.
/// </summary>
[Authorize(Roles = "Operador,Coordinador,Administrador")]
public sealed class ReportesController(RescautaApiClient api) : Controller
{
    private const int TamanoPagina = 20;

    public async Task<IActionResult> Index(
        Guid? comedorId,
        string? desde,
        string? hasta,
        int pagina = 1,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        var filtroDesde = ParseFecha(desde, inicioDeDia: true);
        var filtroHasta = ParseFecha(hasta, inicioDeDia: false);
        string? error = null;

        if ((filtroDesde is null && !string.IsNullOrWhiteSpace(desde)) ||
            (filtroHasta is null && !string.IsNullOrWhiteSpace(hasta)))
        {
            error = "El rango de fechas no es válido. Se muestran las donaciones sin ese filtro.";
            desde = null;
            hasta = null;
            filtroDesde = null;
            filtroHasta = null;
        }
        else if (filtroDesde > filtroHasta)
        {
            error = "La fecha inicial no puede ser posterior a la fecha final. Se muestran las donaciones sin ese filtro.";
            desde = null;
            hasta = null;
            filtroDesde = null;
            filtroHasta = null;
        }

        var reporteDonacionesTask = api.ObtenerReporteDonacionesAsync(cancellationToken);
        var reporteStockTask = api.ObtenerReporteStockAsync(cancellationToken);
        var listadoTask = api.BuscarDonacionesAsync(
            new DonacionFiltroRequest
            {
                ComedorId = comedorId,
                Desde = filtroDesde,
                Hasta = filtroHasta,
                Pagina = pagina,
                TamanoPagina = TamanoPagina
            },
            cancellationToken);
        var comedoresTask = api.ObtenerComedoresAsync(cancellationToken);

        await Task.WhenAll(reporteDonacionesTask, reporteStockTask, listadoTask, comedoresTask);

        var reporteDonaciones = await reporteDonacionesTask;
        var reporteStock = await reporteStockTask;
        var listado = await listadoTask;
        var comedores = await comedoresTask;

        // Los fallos de los endpoints de reportes y del directorio se agrupan en un solo
        // aviso y se deduplican: si la API esta caida los tres devuelven el mismo mensaje
        // y el operador no necesita leerlo tres veces.
        var fallos = new[]
        {
            reporteDonaciones.Exito ? null : reporteDonaciones.Mensaje,
            reporteStock.Exito ? null : reporteStock.Mensaje,
            comedores.Exito ? null : comedores.Mensaje
        }
        .Where(mensaje => mensaje is not null)
        .Distinct()
        .ToList();

        if (fallos.Count > 0)
        {
            var detalle = string.Join(" ", fallos);
            error = error is null ? detalle : $"{error} {detalle}";
        }

        return View(new ReportesViewModel
        {
            DonacionesRecibidas = reporteDonaciones.Exito ? reporteDonaciones.Valor : null,
            StockDisponible = reporteStock.Exito ? reporteStock.Valor : null,
            Donaciones = listado.Exito ? listado.Valor!.Items : [],
            Comedores = comedores.Exito ? FiltrarComedores(comedores.Valor!) : [],
            ComedorId = comedorId,
            Desde = string.IsNullOrWhiteSpace(desde) ? null : desde.Trim(),
            Hasta = string.IsNullOrWhiteSpace(hasta) ? null : hasta.Trim(),
            Pagina = listado.Exito ? listado.Valor!.PageNumber : pagina,
            TotalCount = listado.Exito ? listado.Valor!.TotalCount : 0,
            TotalPages = listado.Exito ? listado.Valor!.TotalPages : 0,
            HasPrevious = listado.Exito && listado.Valor!.HasPrevious,
            HasNext = listado.Exito && listado.Valor!.HasNext,
            Error = error,
            ErrorListado = listado.Exito ? null : listado.Mensaje
        });
    }

    /// <summary>
    /// Convierte "yyyy-MM-dd" en un DateTimeOffset que cubre el dia completo: desde las
    /// 00:00 para el inicio y hasta las 23:59:59 para el fin, usando el desfase local
    /// del servidor igual que el resto de la aplicacion. Devuelve null si el texto no es
    /// una fecha valida.
    /// </summary>
    private static DateTimeOffset? ParseFecha(string? valor, bool inicioDeDia)
    {
        if (string.IsNullOrWhiteSpace(valor) ||
            !DateTime.TryParseExact(
                valor.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fecha))
        {
            return null;
        }

        var dia = inicioDeDia ? fecha : fecha.AddDays(1).AddSeconds(-1);

        return new DateTimeOffset(dia);
    }

    // El comedor del filtro se elige entre los asignados al usuario: el Administrador ve
    // toda la red y el resto solo sus comedores, igual que el panel y el inventario.
    private IReadOnlyList<ComedorResumen> FiltrarComedores(IReadOnlyList<ComedorResumen> comedores)
    {
        if (User.IsInRole("Administrador"))
        {
            return comedores;
        }

        var asignados = User
            .FindAll("comedor_id")
            .Select(claim => Guid.TryParse(claim.Value, out var comedorId) ? comedorId : Guid.Empty)
            .Where(comedorId => comedorId != Guid.Empty)
            .ToHashSet();

        return comedores.Where(c => asignados.Contains(c.Id)).ToList();
    }
}
