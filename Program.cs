using Microsoft.AspNetCore.HttpOverrides;
using PROYECTO_FINAL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// -----------------------------------------------------------------------------
// Estado entre peticiones: los tres mecanismos que pide el temario y que NO son lo mismo.
//
//   Cookie  -> la guarda el CLIENTE. Visible con F12 > Application > Cookies. 4 KB max.
//   Session -> la guarda el SERVIDOR. El cliente solo recibe un id de sesion. Sin limite
//              practico. Es lo que se usa para el "comedor que estas mirando ahora mismo".
//   TempData-> la guarda el SERVIDOR y se borra DESPUES de leerse. Vive un solo request.
//              Es el canal de los avisos "guardado", "elige un comedor", etc.
//
//   TempData y Session necesitan un IDistributedCache. AddSession lo exige para poder
//   resolver su propio almacenamiento temporal.
// -----------------------------------------------------------------------------
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    // Nombre propio para no chocar con el de la API si un dia comparten dominio.
    options.Cookie.Name = ".Rescauta.Session";
    options.Cookie.HttpOnly = true;

    // SameSite Lax: el boton de atras y los enlaces internos siguen llevando la sesion,
    // pero un formulario de otro sitio no la puede leveraged.
    options.Cookie.SameSite = SameSiteMode.Lax;

    // SecurePolicy: SameAsRequest SIEMPRE. No es por comodidad local: es por como funciona
    // un contenedor detras del proxy de Render, que termina el TLS y reenvia por http. Con
    // Always, ASP.NET lanza excepcion (CheckSSLConfig) al pintar cualquier formulario si la
    // peticion que ve el contenedor no es https, y el navegador del profesor se lleva una
    // pagina 500 en /StateDemo y en el banco. El atributo Secure de la cookie lo decide en
    // el fondo la conexion del navegador (https), no la vision del contenedor.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    // 30 minutos de inactividad es lo razonable para un puesto de operador. Al expirar, la
    // sesion se destruye sola: no hace falta ningun temporizador.
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    // No hay ninguna opcion de "SlidingExpiration" en SessionOptions, y no es un olvido:
    // la cookie de sesion ya ES deslizante de serie. Cada peticion renueva los 30 minutos,
    // asi que un operador que esta trabajando no pierde el comedor seleccionado a mitad de
    // turno, mientras que uno que abandona el puesto la pierde a los 30 minutos.
});

// Cookie de antiforgery. La del Banco de Alimentos depende de ella ([ValidateAntiForgeryToken]),
// asi que su nombre y sus politicas se fijan aqui una sola vez.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".Rescauta.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Cliente de la API de Rescauta. La URL base sale de appsettings.json ("Api:BaseUrl"), no
// esta escrita en el codigo, para poder apuntar a otra maquina sin recompilar.
//
// AddHttpClient y no new HttpClient(): el contenedor se encarga del socket y del tiempo de
// vida, y asi los timeouts se pueden cambiar por configuracion.
builder.Services.AddHttpClient<RescautaApiClient>((servicios, cliente) =>
{
    var configuracion = servicios.GetRequiredService<IConfiguration>();

    var urlBase = configuracion["Api:BaseUrl"];

    if (string.IsNullOrWhiteSpace(urlBase))
    {
        throw new InvalidOperationException(
            "Falta Api:BaseUrl en appsettings.json. Sin ella el MVC no sabe donde esta la API.");
    }

    cliente.BaseAddress = new Uri(urlBase);

    // Timeout corto a proposito. Si la API esta caída, el operador no debe quedarse 100
    // segundos mirando una pagina en blanco: en 10 segundos ya tiene el aviso.
    cliente.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// En local el comando es uno solo, "dotnet run", pero el proyecto son dos procesos: el MVC
// (este) y la API, que es la que tiene la base de datos. Si la API no esta levantada, las
// tres pantallas abren y las tres dicen que no pueden contactarla.
//
// Asi que en desarrollo se levanta sola como proceso hijo y se espera a que responda, para
// que "dotnet run" deje las tres pantallas funcionando. En produccion NO: alli la API es
// otro servicio de Render con su propia URL, y este arranque solo taparia un problema de
// despliegue. Con --no-api se desactiva, por si se quiere levantar la API a mano.
ApiLocal? apiLocal = null;

if (app.Environment.IsDevelopment() && !args.Contains("--no-api"))
{
    apiLocal = await ApiLocal.IniciarAsync(
        app.Configuration,
        app.Environment.ContentRootPath,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Rescauta.ApiLocal"));

    // ESTE ES EL PASO QUE CONECTA TODO. ApiLocal puede haber movido la API a otro puerto si
    // el configurado estaba ocupado, asi que el BaseUrl real se devuelve y se sobrescribe
    // en la configuracion. Sin esto, el HttpClient seguiria apuntando al puerto viejo y las
    // pantallas dirian que la API no responde.
    //
    // Es seguro hacerlo aqui y no antes: AddHttpClient solo registra el servicio, y el
    // delegate que fija el BaseAddress se ejecuta la primera vez que alguien pide el
    // cliente, que es durante el primer request, ya con este valor puesto.
    app.Configuration["Api:BaseUrl"] = apiLocal.BaseUrl;
}
else if (!app.Environment.IsDevelopment())
{
    // -----------------------------------------------------------------------------
    // ARRANQUE FAIL-FAST EN PRODUCCION: la URL de la API se comprueba aqui y no en el
    // primer request.
    //
    // Sin esta comprobacion, un despliegue con Api__BaseUrl mal puesta NO se rompe:
    // el MVC arranca, el sitio responde 200, y lo unico que aparece es el aviso "No se
    // pudo contactar a la API" en las tres pantallas. Eso es un fallo de despliegue
    // disfrazado de aplicacion funcionando, y se diagnostica tarde y mal: nadie sospecha
    // de la variable de entorno cuando la pagina "carga".
    //
    // Los dos fallos que se comprueban son los dos que ocurren en la practica:
    //   1) La variable no esta definida. Sin ella se usaria el valor de
    //      appsettings.json, que es http://localhost:5266/.
    //   2) Apunta a localhost. Dentro del contenedor, localhost es el PROPIO contenedor
    //      del MVC, que no sirve /api/v1/comedores. El sitio abriria y cada pagina
    //      recibiria un 404 de si mismo, convertido por el cliente en el mismo aviso
    //      generico de "API no disponible".
    // -----------------------------------------------------------------------------
    var urlApi = app.Configuration["Api:BaseUrl"];

    if (string.IsNullOrWhiteSpace(urlApi))
    {
        throw new InvalidOperationException(
            "Falta la variable de entorno Api__BaseUrl. Sin ella el MVC no sabe donde esta la API. " +
            "En Render se rellena en el servicio rescauta-mvc y debe ser la URL publica de " +
            "rescauta-api, con https y con la barra al final, por ejemplo https://rescauta-api.onrender.com/");
    }

    if (!Uri.TryCreate(urlApi, UriKind.Absolute, out var uriApi))
    {
        throw new InvalidOperationException(
            $"Api__BaseUrl no es una URL valida: '{urlApi}'. Debe incluir el esquema, por ejemplo https://rescauta-api.onrender.com/");
    }

    if (uriApi.IsLoopback)
    {
        throw new InvalidOperationException(
            $"Api__BaseUrl apunta a {uriApi.Host}, que dentro del contenedor es este mismo servicio del MVC, " +
            "no la API. Hay que poner la URL publica de rescauta-api, con https y con la barra al final.");
    }
}

// Registrar el cierre ANTES de app.Run(), que es donde el proceso empieza a atender.
// ApplicationStopping salta con Ctrl+C, al cerrar la ventana y si el arranque falla. Sin
// esto la API se queda viva ocupando el puerto, y la proxima vez "dotnet run" la encuentra
// responding, no la relanza y se conecta a la version vieja sin avisar.
if (apiLocal is not null)
{
    app.Lifetime.ApplicationStopping.Register(apiLocal.Dispose);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Render termina el TLS en su proxy y reenvia al contenedor por http, anadiendo las
    // cabeceras X-Forwarded-*. Sin UseForwardedHeaders la aplicacion ve cada peticion como
    // http: las cookies de sesion y antiforgery se producen para http, Request.IsHttps da
    // false y los links absolutos se generan con http:// en vez de https://. Se confiando
    // las cabeceras SOLO en produccion, donde el unico que las pone es el proxy de Render.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// La redireccion a https solo tiene sentido cuando hay un puerto https configurado. En el
// perfil "http" de launchSettings.json no lo hay, y activarla ahi solo produce el aviso
// "Failed to determine the https port for redirect" en cada arranque, sin efecto real.
if (app.Urls.Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

// UseSession va DESPUES de UseRouting y ANTES de mapear los controllers: si se pusiera
// antes de UseRouting, HttpContext no tendria todavia su ruta resuelta y la sesion no se
// asociaria bien. Tampoco puede ir despues de MapControllerRoute, porque ahi los endpoints
// ya se ejecutaron.
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

// -----------------------------------------------------------------------------
// Health check del MVC para Render: /healthz.
//
// Depende de NADA. No mira la sesion, ni la base de datos, ni la API. Es lo contrario a
// "/" , que en cada carga llama a la API para pintar el mapa, y eso importa mas de lo que
// parece en el plan gratis de Render: el servicio se suspende a los 15 minutos sin
// trafico, asi que la primera visita despierte a la API. Si ademas el health check fuera
// una de esas paginas, Render mediria un arranque en frio, lo daria por vencido y
// reiniciaria el servicio, que al reiniciar vuelverse a suspender... y el sitio se
// quedaria levantandose y tumbandose solo.
//
// Ademas, este endpoint es util en local para comprobar que el MVC arranca sin tener que
// depender de que la API este viva.
// -----------------------------------------------------------------------------
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
