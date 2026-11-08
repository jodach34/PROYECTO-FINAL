using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Services;

public sealed class RescautaApiClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RescautaApiClient> logger)
{
    public const string ApiNoDisponible =
        "No se pudo contactar a la API de Rescauta. Ensure que este corriendo en otra terminal.";

    public const string ApiSesionExpirada =
        "Tu sesión expiró. Vuelve a iniciar sesión para continuar.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? TokenActual =>
        httpContextAccessor.HttpContext?.Session.GetString(AuthSessionKeys.Token);

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

    public async Task<Result<InsumoRegistrado>> CrearInsumoAsync(
        CrearInsumoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await SendAsync<InsumoRegistrado>(
            HttpMethod.Post,
            "/api/v1/kardex/insumos",
            request,
            cancellationToken);
    }

    public async Task<Result<MovimientoKardexResult>> RegistrarMovimientoAsync(
        MovimientoKardexRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await SendAsync<MovimientoKardexResult>(
            HttpMethod.Post,
            "/api/v1/kardex/movimiento",
            request,
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

    public async Task<Result<IReadOnlyList<DonacionRegistrada>>> ObtenerMisDonacionesAsync(
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<DonacionRegistrada>>(
            $"/api/v1/donaciones/mias?limite={limite}",
            cancellationToken);
    }

    public async Task<Result<PagedResultApi<DonacionRegistrada>>> BuscarDonacionesAsync(
        DonacionFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var parametros = new List<string>
        {
            $"pagina={filtro.Pagina}",
            $"tamanoPagina={filtro.TamanoPagina}"
        };

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            parametros.Add($"estado={Uri.EscapeDataString(filtro.Estado)}");
        }

        if (filtro.ComedorId is { } comedorId)
        {
            parametros.Add($"comedorId={comedorId}");
        }

        if (filtro.InsumoId is { } insumoId)
        {
            parametros.Add($"insumoId={insumoId}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            parametros.Add($"busqueda={Uri.EscapeDataString(filtro.Busqueda.Trim())}");
        }

        if (filtro.Desde is { } desde)
        {
            parametros.Add($"desde={Uri.EscapeDataString(desde.ToString("O"))}");
        }

        if (filtro.Hasta is { } hasta)
        {
            parametros.Add($"hasta={Uri.EscapeDataString(hasta.ToString("O"))}");
        }

        return await GetAsync<PagedResultApi<DonacionRegistrada>>(
            $"/api/v1/donaciones/gestion?{string.Join("&", parametros)}",
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> ObtenerDonacionAsync(
        Guid donacionId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<DonacionRegistrada>(
            $"/api/v1/donaciones/{donacionId}",
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<DonacionHistorialApi>>> ObtenerHistorialDonacionAsync(
        Guid donacionId,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<DonacionHistorialApi>>(
            $"/api/v1/donaciones/{donacionId}/historial",
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> ConfirmarDonacionAsync(
        Guid donacionId,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<DonacionRegistrada>(
            HttpMethod.Post,
            $"/api/v1/donaciones/{donacionId}/confirmar",
            null,
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> MarcarDonacionEnTransitoAsync(
        Guid donacionId,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<DonacionRegistrada>(
            HttpMethod.Post,
            $"/api/v1/donaciones/{donacionId}/en-transito",
            null,
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> EntregarDonacionAsync(
        Guid donacionId,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<DonacionRegistrada>(
            HttpMethod.Post,
            $"/api/v1/donaciones/{donacionId}/entregar",
            null,
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> CancelarDonacionAsync(
        Guid donacionId,
        string motivo,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<DonacionRegistrada>(
            HttpMethod.Post,
            $"/api/v1/donaciones/{donacionId}/cancelar",
            new MotivoDonacionRequest { Motivo = motivo },
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> AnularDonacionAsync(
        Guid donacionId,
        string motivo,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<DonacionRegistrada>(
            HttpMethod.Post,
            $"/api/v1/donaciones/{donacionId}/anular",
            new MotivoDonacionRequest { Motivo = motivo },
            cancellationToken);
    }

    public async Task<Result> EliminarDonacionAsync(
        Guid donacionId,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Delete,
            $"/api/v1/donaciones/{donacionId}",
            null,
            cancellationToken);
    }

    public async Task<Result<DonacionRegistrada>> RegistrarDonacionAsync(
        DonacionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await SendAsync<DonacionRegistrada>(
            HttpMethod.Post,
            "/api/v1/donaciones",
            request,
            cancellationToken);
    }

    public async Task<Result<SeguimientoDonacionPublico>> ObtenerSeguimientoDonacionAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<SeguimientoDonacionPublico>(
            $"/api/v1/donaciones/seguimiento/{Uri.EscapeDataString(codigo)}",
            cancellationToken);
    }

    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<LoginResponse>(
            HttpMethod.Post,
            "/api/v1/auth/login",
            request,
            cancellationToken);
    }

    public async Task<Result<UsuarioSesion>> RegistrarDonanteAsync(
        RegistroDonanteRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<UsuarioSesion>(
            HttpMethod.Post,
            "/api/v1/auth/registro/donante",
            request,
            cancellationToken);
    }

    public async Task<Result<UsuarioSesion>> SolicitarOperadorAsync(
        SolicitudOperadorRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<UsuarioSesion>(
            HttpMethod.Post,
            "/api/v1/auth/registro/operador",
            request,
            cancellationToken);
    }

    public async Task<Result<RecuperacionResponse>> RecuperarPasswordAsync(
        RecuperarPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<RecuperacionResponse>(
            HttpMethod.Post,
            "/api/v1/auth/recuperar",
            request,
            cancellationToken);
    }

    public async Task<Result> RestablecerPasswordAsync(
        RestablecerPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Post,
            "/api/v1/auth/restablecer",
            request,
            cancellationToken);
    }

    public async Task<Result> CambiarPasswordAsync(
        CambiarPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Post,
            "/api/v1/auth/cambiar-password",
            request,
            cancellationToken);
    }

    public async Task<Result> LogoutAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Post,
            "/api/v1/auth/logout",
            null,
            cancellationToken);
    }

    public async Task<Result<UsuarioSesion>> ObtenerSesionAsync(
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<UsuarioSesion>(
            HttpMethod.Get,
            "/api/v1/auth/me",
            null,
            cancellationToken,
            token);
    }

    public async Task<Result<IReadOnlyList<AdminUsuario>>> AdminListarUsuariosAsync(
        string? busqueda,
        CancellationToken cancellationToken = default)
    {
        var ruta = string.IsNullOrWhiteSpace(busqueda)
            ? "/api/v1/admin/usuarios"
            : $"/api/v1/admin/usuarios?busqueda={Uri.EscapeDataString(busqueda.Trim())}";

        return await GetAsync<IReadOnlyList<AdminUsuario>>(ruta, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SolicitudOperadorAdmin>>> AdminSolicitudesAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<SolicitudOperadorAdmin>>(
            "/api/v1/admin/usuarios/solicitudes",
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ComedorOpcion>>> AdminComedoresAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<IReadOnlyList<ComedorOpcion>>(
            "/api/v1/admin/usuarios/comedores",
            cancellationToken);
    }

    public async Task<Result> AdminAprobarAsync(
        Guid usuarioId,
        AprobarOperadorRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Post,
            $"/api/v1/admin/usuarios/{usuarioId}/aprobar",
            request,
            cancellationToken);
    }

    public async Task<Result> AdminRechazarAsync(
        Guid usuarioId,
        RechazarSolicitudRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Post,
            $"/api/v1/admin/usuarios/{usuarioId}/rechazar",
            request,
            cancellationToken);
    }

    public async Task<Result> AdminCambiarRolAsync(
        Guid usuarioId,
        CambiarRolRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Put,
            $"/api/v1/admin/usuarios/{usuarioId}/rol",
            request,
            cancellationToken);
    }

    public async Task<Result> AdminCambiarEstadoAsync(
        Guid usuarioId,
        CambiarEstadoUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Put,
            $"/api/v1/admin/usuarios/{usuarioId}/estado",
            request,
            cancellationToken);
    }

    public async Task<Result> AdminAsignarComedoresAsync(
        Guid usuarioId,
        ActualizarAsignacionesRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithoutResponseAsync(
            HttpMethod.Put,
            $"/api/v1/admin/usuarios/{usuarioId}/comedores",
            request,
            cancellationToken);
    }

    private async Task<Result<T>> GetAsync<T>(string ruta, CancellationToken cancellationToken)
    {
        return await SendAsync<T>(HttpMethod.Get, ruta, null, cancellationToken);
    }

    private async Task<Result<T>> SendAsync<T>(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        CancellationToken cancellationToken,
        string? tokenExplicito = null)
    {
        try
        {
            using var solicitud = CrearSolicitud(metodo, ruta, cuerpo, tokenExplicito);
            using var respuesta = await httpClient.SendAsync(solicitud, cancellationToken);

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

    private async Task<Result> SendWithoutResponseAsync(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        CancellationToken cancellationToken)
    {
        try
        {
            using var solicitud = CrearSolicitud(metodo, ruta, cuerpo, null);
            using var respuesta = await httpClient.SendAsync(solicitud, cancellationToken);

            if (respuesta.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return await TraducirErrorAsync(respuesta, cancellationToken);
        }
        catch (Exception ex) when (EsFalloDeConexion(ex))
        {
            logger.LogWarning(ex, "Fallo de red al llamar a {Ruta}.", ruta);

            return Result.Failure("api_no_disponible", ApiNoDisponible);
        }
    }

    private HttpRequestMessage CrearSolicitud(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        string? tokenExplicito)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);
        var token = tokenExplicito ?? TokenActual;

        if (!string.IsNullOrWhiteSpace(token))
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (cuerpo is not null)
        {
            solicitud.Content = new StringContent(
                JsonSerializer.Serialize(cuerpo, Json),
                Encoding.UTF8,
                "application/json");
        }

        return solicitud;
    }

    private async Task<Result<T>> TraducirErrorAsync<T>(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            httpContextAccessor.HttpContext?.Session.Remove(AuthSessionKeys.Token);
            return Result<T>.Failure("api_no_autorizado", ApiSesionExpirada);
        }

        var error = await LeerErrorAsync(respuesta, cancellationToken);

        var detalle = string.IsNullOrWhiteSpace(error?.Detalle)
            ? $"La API respondio HTTP {(int)respuesta.StatusCode} ({respuesta.StatusCode})."
            : error!.Detalle;

        return Result<T>.Failure(
            error?.Titulo ?? $"http_{(int)respuesta.StatusCode}",
            detalle);
    }

    private async Task<Result> TraducirErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            httpContextAccessor.HttpContext?.Session.Remove(AuthSessionKeys.Token);
            return Result.Failure("api_no_autorizado", ApiSesionExpirada);
        }

        var error = await LeerErrorAsync(respuesta, cancellationToken);

        var detalle = string.IsNullOrWhiteSpace(error?.Detalle)
            ? $"La API respondio HTTP {(int)respuesta.StatusCode} ({respuesta.StatusCode})."
            : error!.Detalle;

        return Result.Failure(
            error?.Titulo ?? $"http_{(int)respuesta.StatusCode}",
            detalle);
    }

    private static async Task<ApiError?> LeerErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<ApiError>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool EsFalloDeConexion(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException;
}

public sealed class Result
{
    private Result(bool exito, string? codigo, string? mensaje)
    {
        Exito = exito;
        Codigo = codigo;
        Mensaje = mensaje;
    }

    public bool Exito { get; }

    public string? Codigo { get; }

    public string? Mensaje { get; }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string codigo, string mensaje) => new(false, codigo, mensaje);
}

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
