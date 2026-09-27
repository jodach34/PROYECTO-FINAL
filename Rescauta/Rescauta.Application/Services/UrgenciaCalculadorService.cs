using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;

namespace Rescauta.Application.Services;

/// <inheritdoc cref="IUrgenciaCalculadorService" />
public sealed class UrgenciaCalculadorService : IUrgenciaCalculadorService
{
    /// <summary>Umbral de dias a partir del cual un insumo pasa a emergencia.</summary>
    private const int DiasEmergencia = 2;

    /// <summary>Umbral de dias a partir del cual un insumo pasa a alerta.</summary>
    private const int DiasAlerta = 5;

    public int CalcularDiasRestantes(decimal stockActual, decimal consumoDiarioEstimado)
    {
        if (stockActual <= 0m)
        {
            return 0;
        }

        if (consumoDiarioEstimado <= 0m)
        {
            return 0;
        }

        // Redondeo hacia abajo a proposito: 4.9 dias de arroz se muestran como 4, no
        // como 5. Redondear hacia arriba diria "5 dias" cuando en realidad no alcanza
        // para el quinto, que es justo lo que el coordinador necesita saber.
        return (int)Math.Floor(stockActual / consumoDiarioEstimado);
    }

    public EstadoAbastecimiento EvaluarEstado(
        decimal stockActual,
        decimal stockMinimo,
        decimal consumoDiarioEstimado)
    {
        var dias = CalcularDiasRestantes(stockActual, consumoDiarioEstimado);

        if (dias <= DiasEmergencia || stockActual <= stockMinimo / 2m)
        {
            return EstadoAbastecimiento.Emergencia;
        }

        if (dias <= DiasAlerta || stockActual <= stockMinimo)
        {
            return EstadoAbastecimiento.Alerta;
        }

        return EstadoAbastecimiento.Abastecido;
    }

    public EvaluacionUrgencia Evaluar(Insumo insumo)
    {
        ArgumentNullException.ThrowIfNull(insumo);

        return new EvaluacionUrgencia(
            CalcularDiasRestantes(insumo.StockActual, insumo.ConsumoDiarioEstimado),
            EvaluarEstado(insumo.StockActual, insumo.StockMinimo, insumo.ConsumoDiarioEstimado),
            insumo.StockActual,
            insumo.ConsumoDiarioEstimado);
    }

    public EvaluacionUrgencia EvaluarComedor(IReadOnlyList<Insumo> insumos)
    {
        ArgumentNullException.ThrowIfNull(insumos);

        if (insumos.Count == 0)
        {
            // Un comedor sin insumos registrados no es una emergencia: todavia no se
            // sabe nada de el. Se reporta como Abastecido para no pintarlo de rojo por
            // falta de datos.
            return new EvaluacionUrgencia(0, EstadoAbastecimiento.Abastecido, 0m, 0m);
        }

        // El estado del comedor es el peor de sus insumos, no el promedio: promediar
        // dejaria en verde un comedor al que le falta el insumo principal.
        return insumos
            .Select(Evaluar)
            .OrderBy(evaluacion => evaluacion.Estado)
            .ThenBy(evaluacion => evaluacion.DiasRestantes)
            .First();
    }
}
