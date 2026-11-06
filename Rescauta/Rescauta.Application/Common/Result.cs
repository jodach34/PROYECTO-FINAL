namespace Rescauta.Application.Common;

/// <summary>
/// Resultado de un caso de uso. Evita excepciones como control de flujo de negocio y
/// permite que la API traduzca el resultado a un codigo HTTP sin conocer el caso de uso.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

    public static Result<T> Success(T value) => new(true, value, null, null);

    public new static Result<T> Failure(string errorCode, string errorMessage) =>
        new(false, default, errorCode, errorMessage);

    public static Result<T> Failure(string errorCode, string errorMessage, T value) =>
        new(false, value, errorCode, errorMessage);
}
