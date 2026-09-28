using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Levanta la API como proceso hijo en desarrollo y se encarga de que no deje basura.
///
/// El proyecto quedo partido en dos: el MVC (este) y la API, que es la que habla con
/// SQLite. Render levanta un proceso por servicio, pero en local el comando es uno:
/// "dotnet run". Sin esto, el MVC abria las tres pantallas y las tres mostraban el aviso
/// de que la API no responde.
///
/// PROBLEMA QUE RESUELVE, y por que este archivo exists:
///
///   1) HUERFANOS. La API se lanzaba con Process.Start y nunca se terminaba. Al cerrar el
///      MVC con Ctrl+C, la API seguia corriendo, con el puerto ocupado. La segunda vez que
///      se hacia "dotnet run", el puerto ya estaba abierto, la comprobacion de "esta la API
///      levantada?" daba que si, y no se relanzaba NADA: el MVC se conectaba a la API vieja
///      en silencio. De ahi el sintoma de "el puerto ya lo tiene otro" y el ARRANCQUE en
///      falso, que es peor que un fallo visible.
///      Aqui el hijo se registra en IHostApplicationLifetime y se mata con
///      entireProcessTree, asi que se lleva por delante tambien al "dotnet run" anidado.
///
///   2) PUERTOS OCUPADOS POR OTRO. Si el puerto configurado lo tiene algo que no es
///      nuestro, en vez de reventar con "address already in use" se busca el siguiente
///      libre y se arranca ahi. El MVC se entera porque este metodo devuelve la URL real.
///
///   3) CODIGO VIEJO. Antes de arrancar se limpian los Rescauta.Api que sigan vivos y
///      pertenezcan a ESTE proyecto. Solo a estos: el filtro es por ruta del ejecutable,
///      asi que no se toca ninguna otra API de la maquina.
///
/// Solo aplica en Development. En produccion la API es OTRO servicio con su propia URL, y
/// esto no debe intentar levantarla: si no esta, el problema es de despliegue y hay que
/// verlo en los logs, no escondido detras de un proceso hijo. Se desactiva con --no-api,
/// para cuando se quiere levantar la API a mano con otra configuracion.
/// </summary>
public sealed class ApiLocal : IDisposable
{
    /// <summary>
    /// Espera maxima a que la API responda. 60 segundos es lo que tarda un "dotnet run" en
    /// compilar las 4 capas y arrancar; con menos, el arranque se cancela y el operador ve
    /// un error confuso de timeout en vez de "todavia arrancando".
    /// </summary>
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(60);

    private readonly ILogger logger;
    private Process? proceso;

    private ApiLocal(string baseUrl, ILogger logger)
    {
        BaseUrl = baseUrl;
        this.logger = logger;
    }

    /// <summary>URL con la que el MVC debe hablar con la API, con el puerto ya resuelto.</summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Arranca la API si hace falta y devuelve el caso. No lanza excepcion si la API no
    /// levanta: las pantallas igual van a explicar que no esta, que es un mensaje mejor que
    /// una excepcion de arranque sin contexto.
    ///
    /// Si ya hay una API nuestra responding en el puerto pedido, se reutiliza y NO se
    /// relanza. Es el caso de "--no-api" con la API ya abierta a mano en otra terminal.
    /// </summary>
    public static async Task<ApiLocal> IniciarAsync(
        IConfiguration configuracion,
        string contenidoRaiz,
        ILogger logger)
    {
        var urlConfigurada = configuracion["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Falta Api:BaseUrl en appsettings.json.");

        var proyecto = Path.Combine(contenidoRaiz, "Rescauta", "Rescauta.Api", "Rescauta.Api.csproj");

        if (!File.Exists(proyecto))
        {
            logger.LogWarning("No se encontro {Proyecto}. La API no se levanta automaticamente.", proyecto);

            return new ApiLocal(urlConfigurada, logger);
        }

        // 1) Puera de salida: si YA hay algo escuchando en el puerto configurado y es
        //    nuestra API, se respeta. Forzar el relanzamiento mataria el que el dev acaba
        //    de arrancar a mano en otra terminal con otra configuracion.
        if (await HayServidorAsync(urlConfigurada))
        {
            logger.LogInformation("Ya hay una API respondiendo en {Url}. Se reutiliza.", urlConfigurada);

            return new ApiLocal(urlConfigurada, logger);
        }

        // 2) Limpieza de huerfanos de corridas anteriores. Sin esto, una API vieja sigue
        //    ocupando un puerto y el "dotnet run" nuevo se conecta a ella en silencio.
        var huerfanos = MatarHuerfanosPropios(contenidoRaiz, logger);

        if (huerfanos > 0)
        {
            logger.LogWarning(
                "Se cerraron {Cantidad} proceso(s) Rescauta.Api de corridas anteriores. " +
                "Sin esto, el MVC se conectaria a la API vieja y no se verian los cambios nuevos.",
                huerfanos);
        }

        // 3) Puerto libre. Se parte del configurado y, si algo lo ocupa, se sube hasta
        //    encontrar uno libre, en vez de morir con "address already in use".
        var puerto = await ElegirPuertoLibreAsync(urlConfigurada, logger);
        var urlApi = $"http://localhost:{puerto}";

        if (!urlApi.Equals(urlConfigurada.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "El puerto {Configurado} estaba ocupado. La API se levanta en {Url} en su lugar.",
                new Uri(urlConfigurada).Port,
                urlApi);
        }

        var instancia = new ApiLocal(urlApi, logger);

        instancia.proceso = Lanzar(proyecto, contenidoRaiz, puerto, logger);

        if (instancia.proceso is not null)
        {
            await instancia.EsperarAsync(new Uri(urlApi));
        }

        return instancia;
    }

    /// <summary>
    /// Arranca el hijo. La salida NO se redirige a proposito: asi los logs de la API salen
    /// por esta misma consola y se leen mientras se levanta. Si se redirigiera sin leerla,
    /// el buffer del proceso hijo se llena y la API se cuelga.
    ///
    /// --no-launch-profile es DELIBERADO. Con el perfil de launchSettings.json, la API
    ///_impone_ su propio applicationUrl y el puerto que acabamos de calcular se ignora:
    /// el puerto quedaria en 5266, ocupada o no, y el MVC apuntaria a un sitio donde no hay
    /// nada. Sin perfil, el unico que decide el puerto es este codigo.
    ///
    /// Por lo mismo se pasan las variables a mano, en vez de confiar en el perfil:
    /// ASPNETCORE_ENVIRONMENT para que cargue appsettings.Development.json (y con el, la
    /// cadena de SQLite) y ASPNETCORE_URLS para el puerto.
    /// </summary>
    private static Process? Lanzar(string proyecto, string contenidoRaiz, int puerto, ILogger logger)
    {
        var entorno = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",

            // "localhost" y no una IP fija, A PROPOSITO. Kestrel resuelve el nombre y se
            // ata a todo lo que&Iacute; salga: si se le fuerza 127.0.0.1 y en esta maquina
            // "localhost" resuelve a ::1 (IPv6) antes que a 127.0.0.1, el MVC — que si usa
            // "localhost" — golpea ::1 y no encuentra nada, aunque la API este levantada y
            // sana. Es el fallo que hacia que este arranque tardara 60 s y acabara con un
            // "la API no respondio" mentira.
            // Solo ASPNETCORE_URLS: si ademas se pone ASPNETCORE_HTTP_PORTS, Kestrel avisa
            // "Overriding HTTP_PORTS" y gana URLS, que es justo lo que no se quiere.
            ["ASPNETCORE_URLS"] = $"http://localhost:{puerto}"
        };

        try
        {
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = contenidoRaiz,
                UseShellExecute = false
            };

            info.ArgumentList.Add("run");
            info.ArgumentList.Add("--project");
            info.ArgumentList.Add(proyecto);
            info.ArgumentList.Add("--no-launch-profile");

            // Environment se rellena despues de construir el objeto, no en el inicializador:
            // la propiedad es de solo lectura dentro de un inicializador de objeto.
            foreach (var (clave, valor) in entorno)
            {
                info.Environment[clave] = valor;
            }

            return Process.Start(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo arrancar la API. Abrirla a mano: dotnet run --project {Proyecto}", proyecto);

            return null;
        }
    }

    private async Task EsperarAsync(Uri uri)
    {
        var limite = DateTime.UtcNow + EsperaMaxima;

        while (DateTime.UtcNow < limite)
        {
            if (await HayServidorAsync(uri.AbsoluteUri))
            {
                logger.LogInformation("API disponible en {Url}", uri);

                return;
            }

            // Si el hijo murio, no hay nada que esperar. Chamar hasta el limite daria un
            // error de timeout que oculta el motivo real, que esta en los logs de arriba.
            if (proceso is { HasExited: true })
            {
                logger.LogWarning("La API se detuvo durante el arranque (codigo {Codigo}). Los logs de arriba dicen por que.", proceso.ExitCode);

                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        logger.LogWarning("La API no respondio en {Segundos}s. Las pantallas mostraran el aviso de que no esta disponible.", EsperaMaxima.TotalSeconds);
    }

    /// <summary>
    /// Mata los Rescauta.Api que queden vivos y pertenezcan a ESTE proyecto.
    ///
    /// El filtro es deliberadamente doble: nombre del proceso Y ruta del ejecutable dentro
    /// de la carpeta del repo. Con solo el nombre se mataria la API de otro proyecto que el
    /// dev tenga abierta; con solo la ruta no se tocarian los procesos compilados de verdad.
    ///
    /// entireProcessTree se lleva tambien al "dotnet run" que envuelve al hijo, que si no
    /// se queda vivo como proceso "dotnet" sin puerto.
    /// </summary>
    private static int MatarHuerfanosPropios(string contenidoRaiz, ILogger logger)
    {
        var carpetaRescauta = Path.GetFullPath(Path.Combine(contenidoRaiz, "Rescauta"));
        var cerrados = 0;

        foreach (var p in Process.GetProcessesByName("Rescauta.Api"))
        {
            try
            {
                var ruta = p.MainModule?.FileName;

                if (ruta is null || !Path.GetFullPath(ruta).StartsWith(carpetaRescauta, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                p.Kill(entireProcessTree: true);
                cerrados++;
            }
            catch (Exception ex)
            {
                // Sin permisos o el proceso ya estaba cerrandose. No es motivo para
                // abortar el arranque: el puerto libre se comprueba igualmente despues.
                logger.LogDebug(ex, "No se pudo cerrar un Rescauta.Api previo.");
            }
            finally
            {
                p.Dispose();
            }
        }

        return cerrados;
    }

    /// <summary>
    /// Devuelve el puerto que se va a usar: el configurado si esta libre, y si no, el
    /// siguiente libre a partir de ahi.
    ///
    /// Se prueba de verdad enlazando un socket, no consultando el estado del puerto: la
    /// tabla de conexiones puede decir que un puerto esta libre 200 ms antes de que otro
    /// proceso lo abra.
    /// </summary>
    private static async Task<int> ElegirPuertoLibreAsync(string urlConfigurada, ILogger logger)
    {
        var preferido = int.TryParse(new Uri(urlConfigurada).Port.ToString(), out var p) ? p : 5266;

        for (var candidato = preferido; candidato < preferido + 20; candidato++)
        {
            if (EstaLibre(candidato))
            {
                return candidato;
            }
        }

        // Si los 20 puertos estan ocupados, se deja que el sistema elija uno libre
        // pidiendoselo a un socket con puerto 0. La excepcion es que el sistema puede
        // devolver un puerto libre en IPv4 y ocupado en IPv6, pero es preferible a fallar
        // el arranque por no tener ningun puerto.
        using var temporal = new TcpListener(IPAddress.Loopback, 0);
        temporal.Start();
        var asignado = ((IPEndPoint)temporal.LocalEndpoint).Port;
        temporal.Stop();

        logger.LogWarning("Los puertos {Desde}-{Hasta} estaban ocupados. Se usara el {Asignado}.", preferido, preferido + 19, asignado);

        return asignado;
    }

    /// <summary>
    /// Un puerto esta libre solo si se puede atar en las DOS familias de loopback.
    ///
    /// Esto no es un detalle: en Windows 127.0.0.1:puerto y [::1]:puerto son espacios
    /// independientes, asi que un puerto puede estar libre en IPv4 y ocupado en IPv6. Si
    /// solo se probara IPv4, se elegiria un puerto看起来 libre, la API intentaria atarse
    /// a "localhost", Kestrel chocaria con el proceso IPv6 y el arranque reventaria con
    /// "Failed to bind to address". Con las dos comprobadas, ese caso no se da.
    /// </summary>
    private static bool EstaLibre(int puerto)
    {
        return SePuedeAtar(IPAddress.Loopback, puerto) && SePuedeAtar(IPAddress.IPv6Loopback, puerto);
    }

    private static bool SePuedeAtar(IPAddress direccion, int puerto)
    {
        TcpListener? listener = null;

        try
        {
            listener = new TcpListener(direccion, puerto);
            listener.Start();

            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }

    /// <summary>
    /// Comprueba si la API responde, con una peticion HTTP de verdad y no abriendo un
    /// socket TCP.
    ///
    /// Se hizo con TCP al principio y fue un error: abrir el puerto no significa que la API
    /// atienda, y ademas el chequeo por IP y el "localhost" que usa el MVC no tienen por que
    /// coincidir (IPv4 contra IPv6). Una GET a /health no tiene ninguna de las dos dudas:
    /// es exactamente la pregunta que importa, "responde la API?", hecha por el mismo
    /// camino que usa el MVC.
    ///
    /// Se reintenta porque hay una ventana de un par de segundos entre que el puerto abre
    /// y la API esta capaz de atender. Un solo intento daria un falso negativo y la
    /// conclusion erronea de que hay que relanzarla.
    /// </summary>
    private static async Task<bool> HayServidorAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Endpoint que no depende de la base de datos ni de la cache: /health responde
        // aunque la API este a medio inicializar.
        var urlHealth = new Uri(uri, "health").AbsoluteUri;

        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        for (var intento = 0; intento < 5; intento++)
        {
            try
            {
                using var respuesta = await cliente.GetAsync(urlHealth);

                // Cualquier respuesta, incluso 503, prueba que hay ALGO escuchando y
                // hablando HTTP. Lo que no se puede es una excepcion de conexion.
                return true;
            }
            catch (Exception)
            {
                await Task.Delay(400);
            }
        }

        return false;
    }

    /// <summary>
    /// Cierra la API. Lo invoca IHostApplicationLifetime al hacer Ctrl+C o al cerrarse la
    /// ventana, y es lo que evita que la proxima ejecucion se conecte a una API vieja.
    /// </summary>
    public void Dispose()
    {
        if (proceso is null || proceso.HasExited)
        {
            return;
        }

        try
        {
            proceso.Kill(entireProcessTree: true);
            logger.LogInformation("API local cerrada.");
        }
        catch (Exception ex)
        {
            // Si el hijo ya no existe, no hay nada que hacer y no vale la pena ensuciar la
            // consola al apagar.
            logger.LogDebug(ex, "La API local ya estaba cerrada.");
        }
        finally
        {
            proceso.Dispose();
            proceso = null;
        }
    }
}
