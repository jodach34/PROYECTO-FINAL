namespace Rescauta.Application.Interfaces;

/// <summary>
/// Genera la imagen QR que el donante lleva impresa o muestra en el telefono.
///
/// Vive en Application como abstraccion y se implementa en Infrastructure, porque
/// generar un QR es una dependencia tecnica: la que de verdad decide si se usa una
/// libreria, un servicio web o un placeholder esta en la capa de infraestructura.
///
/// El retorno es un data URI (base64) para que el controller pueda devolverlo ya
/// listo en un <c>GET</c> sin escribir nada en disco.
/// </summary>
public interface IQrGeneratorService
{
    /// <summary>
    /// Codifica <paramref name="contenido"/> y devuelve un data URI PNG.
    /// </summary>
    /// <param name="contenido">Texto a codificar. Para Rescauta es el payload de Donacion.</param>
    /// <param name="pixelsPorModulo">Tamano del QR. Menos de 33px no se escanea.</param>
    Task<string> GenerarDataUriAsync(string contenido, int pixelsPorModulo = 8);

    /// <summary>
    /// Genera el QR de una donacion a partir de su codigo de seguimiento y devuelve
    /// el data URI. Es el atajo que usa el endpoint de registro de donacion.
    /// </summary>
    Task<string> GenerarQrDonacionAsync(
        string codigoSeguimiento,
        CancellationToken cancellationToken = default);
}
