namespace Rescauta.Application.Interfaces;

/// <summary>
/// Genera la imagen QR que el donante muestra en el telefono o imprime para el punto de
/// recogida.
///
/// Vive en Application como abstraccion y se implementa en Infrastructure: la libreria
/// concreta (QRCoder) es una dependencia tecnica, no una regla de negocio.
///
/// El retorno es un data URI PNG para que la API pueda devolverlo listo en el DTO y la
/// vista lo use directo en un &lt;img&gt;, sin escribir archivos ni exponer un endpoint extra.
/// </summary>
public interface IQrGeneratorService
{
    /// <summary>Codifica <paramref name="contenido"/> y devuelve un data URI PNG.</summary>
    Task<string> GenerarDataUriAsync(
        string contenido,
        int pixelsPorModulo = 8,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera el QR publico de una donacion a partir de su codigo de seguimiento.
    /// El contenido solo lleva el codigo y la URL publica de seguimiento: nunca datos
    /// personales del donante ni identificadores internos.
    /// </summary>
    Task<string> GenerarQrDonacionAsync(
        string codigoSeguimiento,
        CancellationToken cancellationToken = default);
}
