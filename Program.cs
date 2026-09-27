using PROYECTO_FINAL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
