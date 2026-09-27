using QRCoder;
using Rescauta.Application.Interfaces;

namespace Rescauta.Infrastructure.Services;

/// <summary>
/// Genera el QR de una donacion con QRCoder y lo devuelve como data URI PNG.
///
/// Por que PNG y no SVG: el QR se imprime y se muestra en un <c>&lt;img src&gt;",
/// y un data URI evita por completo el round-trip al servidor. El SVG se reservaria
/// para el caso de imprimir en plotter.
///
/// El nivel de correccion es Q (25% de recuperacion). Es el equilibrio correcto para
/// este caso: el QR se lee con la camara del telefono, a veces con la pantalla sucia
/// o a contraluz, y Q tolera esa situacion sin agrandar el simbolo. El contenido es
/// corto (unos 60 caracteres), asi que el nivel H no costaria nada, pero Q es lo que
/// queda escaneable a mayor distancia.
/// </summary>
public sealed class QrGeneratorService : IQrGeneratorService
{
    /// <summary>
    /// Pixels por modulo. 8 da un QR de unos 200px para nuestro payload, que se lee
    /// bien en pantalla y en una impresion de 5cm. Menos de 4 no se escanea.
    /// </summary>
    private const int PixelsPorModuloPorDefecto = 8;

    public Task<string> GenerarDataUriAsync(string contenido, int pixelsPorModulo = PixelsPorModuloPorDefecto)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            throw new ArgumentException("El contenido a codificar no puede estar vacio.", nameof(contenido));
        }

        if (pixelsPorModulo is < 4 or > 40)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelsPorModulo),
                pixelsPorModulo,
                "El tamano por modulo debe estar entre 4 y 40.");
        }

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);

        var pngByteQrCode = new PngByteQRCode(qrData);
        var bytes = pngByteQrCode.GetGraphic(pixelsPorModulo);

        return Task.FromResult($"data:image/png;base64,{Convert.ToBase64String(bytes)}");
    }

    /// <summary>
    /// Atajo para el endpoint de donacion. Reutiliza el payload que arma la entidad
    /// <c>Donacion.ObtenerPayloadQr()</c> para que el QR y los datos mostrados en
    /// pantalla nunca se contradigan.
    /// </summary>
    public async Task<string> GenerarQrDonacionAsync(
        string codigoSeguimiento,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            throw new ArgumentException(
                "El codigo de seguimiento es obligatorio para generar el QR.",
                nameof(codigoSeguimiento));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var payload = $"RESCAUTA|{codigoSeguimiento.Trim().ToUpperInvariant()}";

        return await GenerarDataUriAsync(payload, PixelsPorModuloPorDefecto);
    }
}
