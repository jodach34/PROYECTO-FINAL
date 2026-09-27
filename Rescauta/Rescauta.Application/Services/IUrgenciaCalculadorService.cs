using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;

namespace Rescauta.Application.Services;

/// <summary>
/// Resultado del calculo para un insumo: quantos dias de stock quedan y en que
/// estado de abastecimiento cae.
/// </summary>
/// <param name="DiasRestantes">
/// Dias enteros de cobertura. 0 significa "no llega ni a hoy".
/// </param>
/// <param name="Estado">Estado que le corresponde al comedor por este insumo.</param>
/// <param name="StockActual">Stock sobre el que se hizo el calculo.</param>
/// <param name="ConsumoDiarioEstimado">Consumo diario usado como divisor.</param>
public sealed record EvaluacionUrgencia(
    int DiasRestantes,
    EstadoAbastecimiento Estado,
    decimal StockActual,
    decimal ConsumoDiarioEstimado);

/// <summary>
/// Calcula los "dias de stock critico" que muestra el Panel de Control y deriva de ahi
/// el estado de abastecimiento que colorea el mapa.
///
/// Es un servicio de Application y no un metodo de la entidad a proposito: el umbral
/// de dias es una politica de negocio que cambiara (y se negociara con el equipo),
/// no una invariante del modelo. Dejarlo inyectado permite testearlo sin base de datos
/// y cambiarlo sin tocar el Domain.
/// </summary>
public interface IUrgenciaCalculadorService
{
    /// <summary>
    /// Dias de cobertura: <c>stockActual / consumoDiarioEstimado</c>, redondeado hacia
    /// abajo. Si el consumo es cero devuelve 0 en vez de dividir, porque un infinito
    /// en pantalla seria peor que un 0 conservador.
    /// </summary>
    int CalcularDiasRestantes(decimal stockActual, decimal consumoDiarioEstimado);

    /// <summary>
    /// Estado de abastecimiento a partir de los dias de cobertura y del stock minimo.
    /// Reglas, en orden de precedencia:
    ///   1. stock por debajo de la mitad del minimo, o 2 dias o menos  -> Emergencia
    ///   2. stock por debajo del minimo, o 5 dias o menos               -> Alerta
    ///   3. en cualquier otro caso                                      -> Abastecido
    /// </summary>
    EstadoAbastecimiento EvaluarEstado(decimal stockActual, decimal stockMinimo, decimal consumoDiarioEstimado);

    /// <summary>Version de un solo paso que devuelve dias y estado juntos.</summary>
    EvaluacionUrgencia Evaluar(Insumo insumo);

    /// <summary>
    /// Agrega la peor evaluacion de todos los insumos de un comedor. El estado del
    /// comedor es el de su insumo mas critico: si le falta arroz, el punto esta en
    /// emergencia aunque el agua este sobrada.
    /// </summary>
    EvaluacionUrgencia EvaluarComedor(IReadOnlyList<Insumo> insumos);
}
