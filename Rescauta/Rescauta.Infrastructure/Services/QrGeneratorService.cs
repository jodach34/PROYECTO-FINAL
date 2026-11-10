using Microsoft.Extensions.Options;
using QRCoder;
using Rescauta.Application.Interfaces;
using Rescauta.Infrastructure.Options;

namespace Rescauta.Infrastructure.Services;

/// <summary>
/// Genera el QR de una donacion con QRCoder y lo devuelve como data URI PNG.
///
/// Se usa PNG porque el QR se muestra en un &lt;img&gt; y se imprime en papel: un data URI
/// evita el round-trip al servidor y el PNG conserva los modulos nitidos al escalar.
/// El nivel de correccion Q (25%) tolera pantallas sucias o impresion con poco contraste
/// sin agrandar demasiado el simbolo.
/// </summary>
public sealed class QrGeneratorService(IOptions<QrOptions> options) : IQrGeneratorService
{
    /// <summary>
    /// Pixels por modulo. 8 da un QR de unos 200px para el payload de seguimiento, que se
    /// lee bien en pantalla y en una impresion de 5 cm. Menos de 4 no se escanea.
    /// </summary>
    private const int PixelsPorModuloPorDefecto = 8;

    public Task<string> GenerarDataUriAsync(
        string contenido,
        int pixelsPorModulo = PixelsPorModuloPorDefecto,
        CancellationToken cancellationToken = default)
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

        cancellationToken.ThrowIfCancellationRequested();

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);

        var pngByteQrCode = new PngByteQRCode(qrData);
        var bytes = pngByteQrCode.GetGraphic(pixelsPorModulo);

        return Task.FromResult($"data:image/png;base64,{Convert.ToBase64String(bytes)}");
    }

    public Task<string> GenerarQrDonacionAsync(
        string codigoSeguimiento,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            throw new ArgumentException(
                "El codigo de seguimiento es obligatorio para generar el QR.",
                nameof(codigoSeguimiento));
        }

        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();
        var baseUrl = options.Value.SeguimientoBaseUrl?.Trim();

        // El QR contiene la URL publica de seguimiento, no el id interno ni el nombre del
        // donante. Si no hay URL configurada, se usa un payload estable con el codigo.
        var contenido = string.IsNullOrWhiteSpace(baseUrl)
            ? $"RESCAUTA|{codigo}"
            : $"{baseUrl.TrimEnd('/')}?codigo={Uri.EscapeDataString(codigo)}";

        return GenerarDataUriAsync(contenido, PixelsPorModuloPorDefecto, cancellationToken);
    }
}
