using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Panel de Control: metricas de la red y el kardex del comedor que se elija.
///
/// El comedor se elige con el query string (?comedorId=...) y no con un selector en
/// JavaScript, para que la pantalla tenga una URL compartible y funcione con el boton de
/// atras del navegador.
/// </summary>
[Authorize(Roles = "Operador,Coordinador,Administrador")]
public class ControlPanelController(RescautaApiClient api) : Controller
{
    /// <summary>
    /// El limite del kardex se acota en el servidor, no solo en la vista: la API ya valida
    /// que este entre 1 y 200 y devolveria 400 si se mandara otra cosa. Aca se recorta al
    /// rango valido para que un parametro raro no rompa la pantalla.
    /// </summary>
    private const int LimiteKardex = 15;

    public async Task<IActionResult> Index(Guid? comedorId, CancellationToken cancellationToken)
    {
        var resumen = await api.ObtenerComedoresAsync(cancellationToken);

        if (!resumen.Exito)
        {
            return View(new PanelViewModel { Error = resumen.Mensaje });
        }

        // El panel es operativo: Operador y Coordinador solo deben ver los comedores que
        // tienen asignados. El Administrador conserva la vision global de la red.
        var comedores = FiltrarComedores(resumen.Valor!);

        if (comedores.Count == 0)
        {
            return View(new PanelViewModel
            {
                Comedores = comedores,
                Error = "No tienes comedores asignados para operar el panel."
            });
        }

        // Sin comedor elegido se toma el primero de la lista. La API ya los devuelve
        // ordenados por urgencia, asi que el panel abre con lo mas urgente a la vista y no
        // con el primero alfabeticamente.
        string? error = null;
        var elegido = comedorId is { } id
            ? comedores.FirstOrDefault(c => c.Id == id)
            : comedores[0];

        if (elegido is null)
        {
            error = "No tienes permisos para consultar el comedor solicitado.";
            elegido = comedores[0];
        }

        // El detalle y el kardex van en paralelo: son dos llamadas independientes y una
        // detras de otra duplicaria la espera de la mas lenta. Se hacen las dos promesas
        // primero y se esperan despues, porque await sobre un array heterogeneous no
        // compila.
        var detalleTask = api.ObtenerComedorAsync(elegido.Id, cancellationToken);
        var kardexTask = api.ObtenerKardexAsync(elegido.Id, LimiteKardex, cancellationToken);

        await Task.WhenAll(detalleTask, kardexTask);

        var detalle = detalleTask.Result;
        var kardex = kardexTask.Result;

        return View(new PanelViewModel
        {
            Comedores = comedores,
            Comedor = detalle.Exito ? detalle.Valor : null,
            Kardex = kardex.Exito ? kardex.Valor! : [],
            ComedorNoEncontrado = !detalle.Exito,
            Error = error
                ?? (detalle.Exito
                    ? kardex.Exito ? null : kardex.Mensaje
                    : detalle.Mensaje)
        });
    }

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
