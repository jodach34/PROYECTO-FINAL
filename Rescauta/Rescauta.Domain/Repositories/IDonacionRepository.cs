using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;

namespace Rescauta.Domain.Repositories;

/// <summary>
/// Contrato de acceso a datos de <see cref="Donacion"/>.
///
/// <see cref="ExisteCodigoAsync"/> existe por una razon concreta: el codigo de
/// seguimiento se genera en el cliente y se muestra en el QR, asi que dos donantes
/// simultaneos pueden colisionar. Esa comprobacion se hace antes de insertar, y no
/// delegando en una excepcion de indice unico: ese error le llegaria al donante como
/// un 500 sin explicar nada.
/// </summary>
public interface IDonacionRepository
{
    Task<Donacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Donacion?> ObtenerPorCodigoAsync(
        string codigoSeguimiento,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Donacion>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Donaciones en un estado concreto. Ej.: las asignadas y aun no entregadas.</summary>
    Task<IReadOnlyList<Donacion>> ListarPorEstadoAsync(
        EstadoDonacion estado,
        CancellationToken cancellationToken = default);

    /// <summary>Comedores candidatos para una donacion, del mas urgente al mas lejano.</summary>
    Task<IReadOnlyList<Comedor>> ListarComedoresRecomendadosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Comueba que el codigo DON-AAAA-NNNNN este libre antes de insertar.</summary>
    Task<bool> ExisteCodigoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default);

    Task AgregarAsync(Donacion donacion, CancellationToken cancellationToken = default);

    void Actualizar(Donacion donacion);
}
