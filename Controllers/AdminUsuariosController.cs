using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class AdminUsuariosController(RescautaApiClient api) : Controller
{
    public async Task<IActionResult> Index(
        string? busqueda,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Usuarios y permisos";
        ViewData["AppSection"] = "Administración";
        ViewData["AppTitle"] = "Usuarios y permisos";

        var usuarios = await api.AdminListarUsuariosAsync(busqueda, cancellationToken);
        var solicitudes = await api.AdminSolicitudesAsync(cancellationToken);
        var comedores = await api.AdminComedoresAsync(cancellationToken);

        var error = new[] { usuarios.Mensaje, solicitudes.Mensaje, comedores.Mensaje }
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

        return View(new AdminUsuariosViewModel
        {
            Usuarios = usuarios.Exito ? usuarios.Valor! : [],
            Solicitudes = solicitudes.Exito ? solicitudes.Valor! : [],
            Comedores = comedores.Exito ? comedores.Valor! : [],
            Busqueda = busqueda,
            Error = error ?? TempData["Error"] as string,
            Exito = TempData["Exito"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(
        Guid usuarioId,
        List<Guid> comedorIds,
        string? busqueda,
        CancellationToken cancellationToken)
    {
        var resultado = await api.AdminAprobarAsync(
            usuarioId,
            new AprobarOperadorRequest { ComedorIds = comedorIds ?? [] },
            cancellationToken);

        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Exito
            ? "Solicitud aprobada."
            : resultado.Mensaje;

        return RedirectToAction(nameof(Index), new { busqueda });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(
        Guid usuarioId,
        string motivo,
        string? busqueda,
        CancellationToken cancellationToken)
    {
        var resultado = await api.AdminRechazarAsync(
            usuarioId,
            new RechazarSolicitudRequest { Motivo = motivo },
            cancellationToken);

        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Exito
            ? "Solicitud rechazada."
            : resultado.Mensaje;

        return RedirectToAction(nameof(Index), new { busqueda });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarRol(
        Guid usuarioId,
        string rol,
        string? busqueda,
        CancellationToken cancellationToken)
    {
        var resultado = await api.AdminCambiarRolAsync(
            usuarioId,
            new CambiarRolRequest { Rol = rol },
            cancellationToken);

        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Exito
            ? "Rol actualizado."
            : resultado.Mensaje;

        return RedirectToAction(nameof(Index), new { busqueda });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        Guid usuarioId,
        string estado,
        string? busqueda,
        CancellationToken cancellationToken)
    {
        var resultado = await api.AdminCambiarEstadoAsync(
            usuarioId,
            new CambiarEstadoUsuarioRequest { Estado = estado },
            cancellationToken);

        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Exito
            ? "Estado actualizado."
            : resultado.Mensaje;

        return RedirectToAction(nameof(Index), new { busqueda });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarComedores(
        Guid usuarioId,
        List<Guid> comedorIds,
        string? busqueda,
        CancellationToken cancellationToken)
    {
        var resultado = await api.AdminAsignarComedoresAsync(
            usuarioId,
            new ActualizarAsignacionesRequest { ComedorIds = comedorIds ?? [] },
            cancellationToken);

        TempData[resultado.Exito ? "Exito" : "Error"] = resultado.Exito
            ? "Asignaciones actualizadas."
            : resultado.Mensaje;

        return RedirectToAction(nameof(Index), new { busqueda });
    }
}
