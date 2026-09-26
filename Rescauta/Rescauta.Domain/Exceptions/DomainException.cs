namespace Rescauta.Domain.Exceptions;

/// <summary>
/// Excepcion de negocio. La capa API la traduce a HTTP 400/409 y la registra como warning,
/// no como error. No usar para fallos de infraestructura (eso es 500).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, params string[] errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; } = [];
}
