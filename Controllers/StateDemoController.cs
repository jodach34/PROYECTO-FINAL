using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

/// <summary>
/// Pagina de demostracion del estado entre peticiones. Es la que muestra, uno al lado del
/// otro, los cuatro mecanismos que el temario pide explicar, para que se vea la diferencia
/// y no solo leerla en un diagrama:
///
///   1. VIEWDATA   - dictionary de la peticion actual. Muere al terminar el request.
///   2. TEMPDATA   - igual, pero sobrevive un redirect (PRG) y se autolimpia al leerse.
///   3. SESSION    - sobrevive muchos requests mientras el navegador mande la cookie de sesion.
///   4. COOKIES    - las unicas que guarda el CLIENTE. Se ven en F12 > Application.
///   5. WEBSOCKET  - canal persistente contra la API, via SignalR (ver wwwroot/js/rescauta-hub.js)
///
/// La Session guarda el "comedor que estas mirando". Es el ejemplo que mas se parece a un
/// caso real: en el Panel de Control el operador cambia de comedor y al volver al mapa
/// quiere seguir en el mismo, no que la pantalla le vuelva al primero de la lista.
///
/// Ojo con TempData: se LEE a mano y por eso se autolimapia. Cada TempData["X"] seguido de
/// TempData.Keep("X") en la misma peticion lo conserva para el siguiente. Si se lee y no se
/// marca Keep, desaparece, que es justo lo que se quiere para un aviso de una sola vez.
/// </summary>
public class StateDemoController(RescautaApiClient api) : Controller
{
    private readonly RescautaApiClient _api = api;

    // Controller NO tiene una propiedad Session. Hay que entrar por HttpContext, igual que
    // se hace con Request y Response. Cachearlo en un campo por peticion evita repetir
    // HttpContext.Session cinco veces en el mismo metodo.
    private ISession Sesion => HttpContext.Session;

    /// <summary>Claves de cookie. Constantes y no literales en las vistas: cambiar el nombre en
    /// un solo sitio.</summary>
    private const string CookieVisitas = "Rescauta.Visitas";
    private const string CookieNombre = "Rescauta.Operador";
    private const string CookieUltimaVista = "Rescauta.UltimaVista";

    private const string SessionComedor = "ComedorActivo";
    private const string SessionTurno = "TurnoOperador";

    /// <summary>
    /// GET: pinta el estado actual y, de paso, lo incrementa. Es la accion que "abre" la
    /// sesion la primera vez: no hay que tener una pantalla aparte para crearla.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // --- 4. COOKIES: leer y escribir ------------------------------------------------
        // La cookie es un string. Lo que se quiera guardar se serializa a mano; aqui es un
        // entero, asi que basta con int.TryParse. Para un objeto usaria JSON.
        var visitas = LeerCookieEntero(CookieVisitas) + 1;

        EscribirCookie(CookieVisitas, visitas.ToString(), 8);
        EscribirCookie(CookieUltimaVista, DateTime.Now.ToString("HH:mm:ss"), 8);

        // El nombre del operador es una cookie de "preferencias": se escribe una vez y el
        // formulario la lee sola en cada visita. Por eso se rellena el campo con el valor
        // guardado, en vez de dejarlo vacio siempre.
        ViewData["OperadorGuardado"] = Request.Cookies[CookieNombre] ?? string.Empty;

        // --- 3. SESSION: crear la sesion en la primera visita -----------------------------
        // La sesion se crea sola al escribir en ella. En esta pantalla se usa para llevar la
        // cuenta de visitas del navegador y el turno: datos del servidor, de tamano
        // arbitrario, que el cliente no tiene por que ver.
        var visitaServidor = (Sesion.GetInt32("VisitasServidor") ?? 0) + 1;
        Sesion.SetInt32("VisitasServidor", visitaServidor);

        if (string.IsNullOrEmpty(Sesion.GetString(SessionTurno)))
        {
            Sesion.SetString(SessionTurno, "Manana");
        }

        // --- 2. TEMPDATA: el aviso de la peticion anterior -------------------------------
        // Solo se pinta si existe, y al leerlo se marca Keep para que sobreviva al render.
        // Si no se hiciera Keep, el aviso se veria una vez y no volveria a aparecer nunca,
        // que es el fallo clasico al trabajar con TempData.
        var aviso = TempData["Aviso"] as string;

        if (aviso is not null)
        {
            TempData.Keep("Aviso");
        }

        // --- 1. VIEWDATA: solo esta peticion ----------------------------------------------
        // El titulo lo usan todas las vistas a traves del _RescautaHead.
        ViewData["Title"] = "Estado y Sesion";
        ViewData["ApiBaseUrl"] = ResolveApiBaseUrl();

        return View(new StateDemoViewModel
        {
            VisitaActual = visitas,
            VisitasServidor = visitaServidor,
            Turno = Sesion.GetString(SessionTurno) ?? "(sin turno)",
            ComedorActivo = Sesion.GetString(SessionComedor) ?? "(ninguno)",
            OperadorGuardado = Request.Cookies[CookieNombre] ?? string.Empty,
            UltimaVisita = Request.Cookies[CookieUltimaVista] ?? "(primera visita)",
            Aviso = aviso,
            Cookies = Request.Cookies.ToDictionary(c => c.Key, c => c.Value),
            InsumoDemo = await ObtenerInsumoDemoAsync(cancellationToken)
        });
    }

    /// <summary>
    /// Busca un insumo real para el boton de prueba en vivo. Devuelve null si la API no
    /// responde: esta pagina es una demostracion de estado entre peticiones, no de la API,
    /// asi que una API caida no debe romperla ni dejar un boton que va a fallar.
    /// </summary>
    private async Task<Guid?> ObtenerInsumoDemoAsync(CancellationToken cancellationToken)
    {
        var comedores = await _api.ObtenerComedoresAsync(cancellationToken);

        if (!comedores.Exito)
        {
            return null;
        }

        foreach (var resumen in comedores.Valor!)
        {
            var detalle = await _api.ObtenerComedorAsync(resumen.Id, cancellationToken);

            var insumo = detalle.Exito ? detalle.Valor!.Inventario.FirstOrDefault() : null;

            if (insumo is not null)
            {
                ViewData["InsumoDemoNombre"] = $"{resumen.Nombre} - {insumo.Nombre} ({insumo.UnidadMedida})";

                return insumo.Id;
            }
        }

        return null;
    }


    /// <summary>
    /// POST: escribe un aviso en TempData y redirige (PRG). Es el patron que se usa para
    /// "guardado", "elige un comedor" y todo lo demas.
    ///
    /// El redirect es lo que hace especial a TempData: si en vez de redirigir se repintara la
    /// vista, el aviso se perderia, porque TempData se limpia en cuanto se lee. El POST
    /// escribe, el redirect GET lo lee. Por eso se llama PRG.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Guardar(string? operador, string? turno, string? comedorId)
    {
        if (!string.IsNullOrWhiteSpace(operador))
        {
            // Cookie: preferencia del cliente, pequena y visible.
            EscribirCookie(CookieNombre, operador.Trim(), 30);
        }

        if (!string.IsNullOrWhiteSpace(turno))
        {
            // Session: estado del servidor.
            Sesion.SetString(SessionTurno, turno.Trim());
        }

        if (!string.IsNullOrWhiteSpace(comedorId) && Guid.TryParse(comedorId, out var id))
        {
            // Session: el comedor que se esta mirando. Sobrevive a la navegacion.
            Sesion.SetString(SessionComedor, id.ToString());
        }

        TempData["Aviso"] = "Cambios guardados. El aviso se leera una sola vez y se borrara solo.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Vacia la sesion. Se incluye para que se vea que Sesion.Clear() borra TODO el estado
    /// de golpe, a diferencia de TempData, que se borra lectura a lectura.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CerrarSesion()
    {
        Sesion.Clear();

        TempData["Aviso"] = "Sesion cerrada. El contador de Session volvio a cero.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Crea la cookie de antiforgery a proposito, sin usarla todavia.
    ///
    /// Razor la genera sola cuando encuentra un &lt;form method="post"&gt; con tag helpers,
    /// asi que normalmente no hace falta llamarla. Se deja expuesta para que se vea el
    /// cookie que respalda el antiforgery, que es un caso de cookie mas.
    /// </summary>
    public IActionResult Token()
    {
        var token = HttpContext.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(HttpContext);

        return Content(
            $"Token antiforgery (cookie .Rescauta.Antiforgery + campo __RequestVerificationToken):\n\n" +
            $"Solicitud: {token.RequestToken}\n\n" +
            $"Cookie:    {token.CookieToken}\n",
            "text/plain");
    }

    private string ResolveApiBaseUrl() =>
        HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Api:BaseUrl"] ?? string.Empty;

    private int LeerCookieEntero(string nombre) =>
        int.TryParse(Request.Cookies[nombre], out var valor) ? valor : 0;

    /// <summary>
    /// Escribe una cookie con las opciones de seguridad correctas. HttpOnly impide que el
    /// JavaScript de la pagina la lea, SameSite Lax evita que viaje en peticiones de otros
    /// sitios, y el Secure depende del entorno igual que en las cookies de sesion.
    ///
    /// IsEssential marca la cookie como imprescindible para la funcionalidad. Sin eso, un
    /// navegador con Cookies bloqueadas las descarta todas, y por eso es el equivalente de
    /// "esta cookie es la que hace que la pagina funcione".
    /// </summary>
    private void EscribirCookie(string nombre, string valor, int dias)
    {
        // Microsoft.AspNetCore.Http.CookieOptions y no System.Net.CookieOptions: los dos
        // existen y tienen el mismo nombre, pero el de ASP.NET es el que entiende
        // Response.Cookies.Append. Sin nameof, el compilador elige el equivocado.
        Response.Cookies.Append(nombre, valor, new Microsoft.AspNetCore.Http.CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(dias),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,

            // Ojo: aqui la propiedad se llama "Secure" y es un bool, no "SecurePolicy".
            // SecurePolicy es de CookieBuilder (que es lo que usa AddSession), mientras
            // que Response.Cookies.Append recibe CookieOptions. Marcarlo solo si la peticion
            // es https: en local, por http, una cookie Secure se descarta y la pantalla
            // pareceria que no guarda nada.
            Secure = HttpContext.Request.IsHttps
        });
    }
}
