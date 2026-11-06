using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Autenticacion;

namespace Rescauta.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpoHtml,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            logger.LogWarning(
                "Email:Host no esta configurado. No se envio el correo a {Destinatario}.",
                destinatario);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromName),
            Subject = asunto,
            Body = cuerpoHtml,
            IsBodyHtml = true
        };

        mensaje.To.Add(destinatario);

        using var cliente = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            cliente.Credentials = new NetworkCredential(_options.User, _options.Password);
        }

        await cliente.SendMailAsync(mensaje, cancellationToken);
    }
}
