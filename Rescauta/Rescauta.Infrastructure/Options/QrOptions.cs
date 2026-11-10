namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "Qr" de appsettings.json.
///
/// El QR de una donacion apunta a la pagina publica de seguimiento del MVC, que corre en
/// otro puerto que la API. Por eso la URL base se configura aqui y no se deduce del host
/// de la peticion: el host de la API no es el host que escanea el donante.
/// </summary>
public sealed class QrOptions
{
    public const string SectionName = "Qr";

    /// <summary>
    /// URL publica de la pagina de seguimiento, sin el parametro "codigo". Es obligatoria:
    /// se define en Qr:SeguimientoBaseUrl o en la variable Qr__SeguimientoBaseUrl. No lleva
    /// localhost por defecto porque el QR se imprime y se escanea fuera del servidor.
    /// </summary>
    public string SeguimientoBaseUrl { get; set; } = string.Empty;
}
