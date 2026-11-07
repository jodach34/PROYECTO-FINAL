using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

[Authorize(Roles = "Operador,Coordinador,Administrador")]
public sealed class InventarioController(
    RescautaApiClient api,
    OpenFoodFactsService openFoodFacts) : Controller
{
    private const int LimiteKardex = 20;

    public async Task<IActionResult> Index(Guid? comedorId, CancellationToken cancellationToken)
    {
        var modelo = await CargarAsync(comedorId, cancellationToken);

        return View(modelo with { Exito = TempData["Exito"] as string });
    }

    /// <summary>
    /// Busca un producto por codigo de barras para autocompletar el formulario de alta.
    /// Devuelve siempre JSON, incluso ante un error, para que el JavaScript muestre el aviso
    /// sin recargar la pagina ni bloquear el registro manual.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> BuscarProducto(
        string? codigo,
        CancellationToken cancellationToken)
    {
        var resultado = await openFoodFacts.BuscarPorCodigoAsync(codigo, cancellationToken);

        if (!resultado.Exito)
        {
            return Json(new ProductoBarcodeRespuesta
            {
                Encontrado = false,
                Motivo = resultado.Codigo,
                Mensaje = resultado.Mensaje
                    ?? "No se pudo consultar el producto. Puedes registrarlo manualmente."
            });
        }

        var producto = resultado.Valor!;

        return Json(new ProductoBarcodeRespuesta
        {
            Encontrado = true,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Marca = producto.Marca,
            Cantidad = producto.Cantidad,
            Ingredientes = producto.Ingredientes,
            ImagenUrl = producto.ImagenUrl,
            Nutricion = producto.Nutricion,
            Mensaje = string.IsNullOrWhiteSpace(producto.Nombre)
                ? "Producto encontrado, pero Open Food Facts no trae su nombre. Completa el campo manualmente."
                : "Producto encontrado. Revisa los datos antes de guardar."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearInsumo(
        InsumoFormViewModel formulario,
        CancellationToken cancellationToken)
    {
        var errores = new List<string>();

        if (formulario.ComedorId is null || formulario.ComedorId == Guid.Empty)
        {
            errores.Add("El comedor es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(formulario.Nombre))
        {
            errores.Add("El nombre del insumo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(formulario.UnidadMedida))
        {
            errores.Add("La unidad de medida es obligatoria.");
        }

        if (formulario.StockInicial < 0m)
        {
            errores.Add("El stock inicial no puede ser negativo.");
        }

        if (formulario.StockMaximo < 0m)
        {
            errores.Add("El stock maximo no puede ser negativo.");
        }

        if (errores.Count > 0)
        {
            var conErrores = await CargarAsync(
                formulario.ComedorId,
                cancellationToken,
                insumoForm: formulario);

            return View("Index", conErrores with { Error = string.Join(" ", errores) });
        }

        var resultado = await api.CrearInsumoAsync(
            new CrearInsumoRequest
            {
                ComedorId = formulario.ComedorId!.Value,
                Nombre = formulario.Nombre.Trim(),
                UnidadMedida = formulario.UnidadMedida.Trim(),
                StockInicial = formulario.StockInicial,
                StockMaximo = formulario.StockMaximo
            },
            cancellationToken);

        if (!resultado.Exito)
        {
            var conError = await CargarAsync(
                formulario.ComedorId,
                cancellationToken,
                insumoForm: formulario);

            return View("Index", conError with { Error = resultado.Mensaje });
        }

        TempData["Exito"] =
            $"Insumo {resultado.Valor!.Nombre} registrado con " +
            $"{resultado.Valor.StockActual:0.###} {resultado.Valor.UnidadMedida} de stock inicial.";

        return RedirectToAction(nameof(Index), new { comedorId = formulario.ComedorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarConsumo(
        ConsumoFormViewModel formulario,
        CancellationToken cancellationToken)
    {
        var errores = new List<string>();

        if (formulario.ComedorId is null || formulario.ComedorId == Guid.Empty)
        {
            errores.Add("El comedor es obligatorio.");
        }

        if (formulario.InsumoId is null || formulario.InsumoId == Guid.Empty)
        {
            errores.Add("Elige el insumo que se consumio.");
        }

        if (formulario.Cantidad <= 0m)
        {
            errores.Add("La cantidad debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(formulario.Responsable))
        {
            errores.Add("El responsable del consumo es obligatorio.");
        }

        if (errores.Count > 0)
        {
            var conErrores = await CargarAsync(
                formulario.ComedorId,
                cancellationToken,
                consumoForm: formulario);

            return View("Index", conErrores with { Error = string.Join(" ", errores) });
        }

        var concepto = string.IsNullOrWhiteSpace(formulario.Concepto)
            ? "Consumo diario"
            : formulario.Concepto.Trim();

        var resultado = await api.RegistrarMovimientoAsync(
            new MovimientoKardexRequest
            {
                InsumoId = formulario.InsumoId!.Value,
                TipoMovimiento = 1,
                Cantidad = formulario.Cantidad,
                Responsable = formulario.Responsable.Trim(),
                Concepto = concepto
            },
            cancellationToken);

        if (!resultado.Exito)
        {
            var conError = await CargarAsync(
                formulario.ComedorId,
                cancellationToken,
                consumoForm: formulario);

            return View("Index", conError with { Error = resultado.Mensaje });
        }

        TempData["Exito"] =
            $"Consumo registrado. Stock actual: " +
            $"{resultado.Valor!.StockActual:0.###} {resultado.Valor.UnidadMedida}.";

        return RedirectToAction(nameof(Index), new { comedorId = formulario.ComedorId });
    }

    private async Task<InventarioViewModel> CargarAsync(
        Guid? comedorId,
        CancellationToken cancellationToken,
        InsumoFormViewModel? insumoForm = null,
        ConsumoFormViewModel? consumoForm = null)
    {
        var resumen = await api.ObtenerComedoresAsync(cancellationToken);

        if (!resumen.Exito)
        {
            return new InventarioViewModel
            {
                Error = resumen.Mensaje,
                Insumo = insumoForm ?? new InsumoFormViewModel(),
                Consumo = consumoForm ?? NuevoConsumoForm()
            };
        }

        var comedores = FiltrarComedores(resumen.Valor!);

        if (comedores.Count == 0)
        {
            return new InventarioViewModel
            {
                Comedores = comedores,
                Error = "No tienes comedores asignados para operar el inventario.",
                Insumo = insumoForm ?? new InsumoFormViewModel(),
                Consumo = consumoForm ?? NuevoConsumoForm()
            };
        }

        string? error = null;
        var elegido = comedorId is { } solicitado
            ? comedores.FirstOrDefault(c => c.Id == solicitado)
            : comedores[0];

        if (elegido is null)
        {
            error = "No tienes permisos para operar sobre el comedor solicitado.";
            elegido = comedores[0];
        }

        var detalleTask = api.ObtenerComedorAsync(elegido.Id, cancellationToken);
        var kardexTask = api.ObtenerKardexAsync(elegido.Id, LimiteKardex, cancellationToken);

        await Task.WhenAll(detalleTask, kardexTask);

        var detalle = detalleTask.Result;
        var kardex = kardexTask.Result;

        var nuevoInsumo = insumoForm ?? new InsumoFormViewModel();
        nuevoInsumo.ComedorId = elegido.Id;

        var nuevoConsumo = consumoForm ?? NuevoConsumoForm();
        nuevoConsumo.ComedorId = elegido.Id;

        if (string.IsNullOrWhiteSpace(nuevoConsumo.Responsable))
        {
            nuevoConsumo.Responsable = User.Identity?.Name ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(nuevoConsumo.Concepto))
        {
            nuevoConsumo.Concepto = "Consumo diario";
        }

        return new InventarioViewModel
        {
            Comedores = comedores,
            Comedor = detalle.Exito ? detalle.Valor : null,
            Kardex = kardex.Exito ? kardex.Valor! : [],
            Error = error
                ?? (detalle.Exito
                    ? kardex.Exito ? null : kardex.Mensaje
                    : detalle.Mensaje),
            Insumo = nuevoInsumo,
            Consumo = nuevoConsumo
        };
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

    private static ConsumoFormViewModel NuevoConsumoForm() =>
        new() { Concepto = "Consumo diario" };
}
