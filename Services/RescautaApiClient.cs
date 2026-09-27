using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Cliente de la API de Rescauta.
///
/// Es un HttpClient con nombre (AddHttpClient), no un new HttpClient() suelto: asi el
/// tiempo de vida y el socket los administra el contenedor y no hay fugas al cambiar de
/// instancia.
///
/// Todas las llamadas devuelven un Result con el motivo del fallo ya en texto. Ningun
/// controller tiene que capturar excepciones de red ni adivinar que hacer cuando la API no
/// arranca: eso lo decide <see cref="ApiNoDisponible"/>.
/// </summary>
public sealed class RescautaApiClient(HttpClient httpClient, ILogger<RescautaApiClient> logger)
{
    /// <summary>
    /// Texto unico para "la API no respondio". Es el caso mas comun durante el desarrollo:
    /// el MVC arranca y la API todavia no, y sin esto la pantalla sale en blanco con un 500
    /// sin ninguna pista.
    /// </summary>
    public const string ApiNoDisponible =
        "No se pudo contactar a la API de Rescauta. Ensure que este corriendo en otra terminal.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<ComedorResumen>>> ObtenerComedoresAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<ComedorResumen>>("/api/v1/comedores", cancellationToken);
    }

    public async Task<Result<ComedorDetalle>> ObtenerComedorAsync(
        Guid comedorId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<ComedorDetalle>($"/api/v1/comedores/{comedorId}", cancellationToken);
    }

    public async Task<Result<IReadOnlyList<KardexFila>>> ObtenerKardexAsync(
        Guid comedorId,
        int limite = 20,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<KardexFila>>(
            $"/api/v1/comedores/{comedorId}/kardex?limite={limite}",
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<DonacionRegistrada>>> ObtenerDonacionesAsync(
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<DonacionRegistrada>>(
            $"/api/v1/donaciones?limite={limite}",
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> RegistrarDonacionAsync(
        DonacionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var respuesta = await httpClient.PostAsJsonAsync(
                "/api/v1/donaciones",
                request,
                Json,
                cancellationToken);

            if (respuesta.IsSuccessStatusCode)
            {
                var donacion = await respuesta.Content
                    .ReadFromJsonAsync<DonacionRegistrada>(Json, cancellationToken);

                return donacion is null
                    ? Result<DonacionRegistrada>.Failure(
                        "respuesta_vacia",
                        "La API devolvio una respuesta vacia al registrar la donacion.")
                    : Result<DonacionRegistrada>.Success(donacion);
            }

            return await TraducirErrorAsync<DonacionRegistrada>(respuesta, cancellationToken);
        }
        catch (Exception ex) when (EsFalloDeConexion(ex))
        {
            logger.LogWarning(ex, "Fallo de red al registrar la donacion.");

            return Result<DonacionRegistrada>.Failure("api_no_disponible", ApiNoDisponible);
        }
    }

    private async Task<Result<T>> GetAsync<T>(string ruta, CancellationToken cancellationToken)
    {
        try
        {
            using var respuesta = await httpClient.GetAsync(ruta, cancellationToken);

            if (respuesta.IsSuccessStatusCode)
            {
                var valor = await respuesta.Content.ReadFromJsonAsync<T>(Json, cancellationToken);

                return valor is null
                    ? Result<T>.Failure("respuesta_vacia", "La API devolvio una respuesta vacia.")
                    : Result<T>.Success(valor);
            }

            return await TraducirErrorAsync<T>(respuesta, cancellationToken);
        }
        catch (Exception ex) when (EsFalloDeConexion(ex))
        {
            logger.LogWarning(ex, "Fallo de red al llamar a {Ruta}.", ruta);

            return Result<T>.Failure("api_no_disponible", ApiNoDisponible);
        }
    }

    /// <summary>
    /// Convierte una respuesta de error de la API en un Result con el motivo que la API
    /// explico. Si el cuerpo no es el ProblemDetails esperado, se usa el status: mejor un
    /// "HTTP 500" en pantalla que un error vacio.
    /// </summary>
    private static async Task<Result<T>> TraducirErrorAsync<T>(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        var error = await respuesta.Content
            .ReadFromJsonAsync<ApiError>(Json, cancellationToken);

        var detalle = string.IsNullOrWhiteSpace(error?.Detalle)
            ? $"La API respondio HTTP {(int)respuesta.StatusCode} ({respuesta.StatusCode})."
            : error!.Detalle;

        return Result<T>.Failure(
            error?.Titulo ?? $"http_{(int)respuesta.StatusCode}",
            detalle);
    }

    /// <summary>
    /// Solo las excepciones de CONEXION y de TIEMPO se convierten en "API no disponible".
    /// Un JsonException o un fallo de logica es un error de este cliente y debe verse como
    /// 500, no disfrazarse de "arranca la API".
    ///
    /// El cancelado va aqui a proposito: si el usuario cierra la pestaña o navega, el
    /// timeout de HttpClient lanza TaskCanceledException y no tiene sentido mostrarle un
    /// error de conexion.
    /// </summary>
    private static bool EsFalloDeConexion(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException;
}

/// <summary>
/// Resultado de una llamada a la API. Es deliberately simple: o hay valor, o hay un codigo
/// y un motivo en texto.
/// </summary>
public sealed class Result<T>
{
    private Result(bool exito, T? valor, string? codigo, string? mensaje)
    {
        Exito = exito;
        Valor = valor;
        Codigo = codigo;
        Mensaje = mensaje;
    }

    public bool Exito { get; }

    public T? Valor { get; }

    public string? Codigo { get; }

    public string? Mensaje { get; }

    public static Result<T> Success(T valor) => new(true, valor, null, null);

    public static Result<T> Failure(string codigo, string mensaje) => new(false, default, codigo, mensaje);
}
