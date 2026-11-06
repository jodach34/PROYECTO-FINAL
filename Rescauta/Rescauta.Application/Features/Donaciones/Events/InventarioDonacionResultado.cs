using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Application.Features.Donaciones.Events;

/// <summary>
/// Resultado compartido entre el caso de uso y el handler del evento. Se registra como
/// Scoped: ambos viven dentro de la misma peticion HTTP, y asi el servicio puede armar el
/// DTO con el saldo real sin volver a consultar el insumo.
///
/// Es intencionalmente mutable: el handler lo completa, el servicio lo lee despues del
/// SaveChanges.
/// </summary>
public sealed class InventarioDonacionResultado
{
    public Guid InsumoId { get; set; }

    public Guid MovimientoId { get; set; }

    public decimal StockPrevio { get; set; }

    public decimal StockActual { get; set; }

    public string InsumoNombre { get; set; } = string.Empty;

    public string UnidadMedida { get; set; } = string.Empty;

    public TipoMovimiento TipoMovimiento { get; set; }

    public decimal Cantidad { get; set; }

    public DateTimeOffset FechaHora { get; set; }

    public bool HuboMovimiento => MovimientoId != Guid.Empty;

    public void Reiniciar()
    {
        InsumoId = Guid.Empty;
        MovimientoId = Guid.Empty;
        StockPrevio = 0m;
        StockActual = 0m;
        InsumoNombre = string.Empty;
        UnidadMedida = string.Empty;
        TipoMovimiento = default;
        Cantidad = 0m;
        FechaHora = default;
    }
}
