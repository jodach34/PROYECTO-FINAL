using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

public sealed class CuentaController(RescautaApiClient api) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "ControlPanel");
        }

        ViewData["Title"] = "Iniciar sesión";

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel formulario,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Iniciar sesión";

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = await api.LoginAsync(
            new LoginRequest
            {
                Correo = formulario.Correo.Trim(),
                Password = formulario.Password,
                Recordarme = formulario.Recordarme
            },
            cancellationToken);

        if (!resultado.Exito)
        {
            formulario.Error = resultado.Mensaje;
            return View(formulario);
        }

        await IniciarSesionAsync(resultado.Valor!, formulario.Recordarme);

        if (!string.IsNullOrWhiteSpace(formulario.ReturnUrl) && Url.IsLocalUrl(formulario.ReturnUrl))
        {
            return LocalRedirect(formulario.ReturnUrl);
        }

        return RedirectToActionDeRol(resultado.Valor!.Usuario.Rol);
    }

    [AllowAnonymous]
    public IActionResult Registro()
    {
        ViewData["Title"] = "Crear cuenta";
        return View(new RegistroViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(
        RegistroViewModel formulario,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Crear cuenta";

        if (!formulario.AceptaTerminos)
        {
            ModelState.AddModelError(nameof(formulario.AceptaTerminos), "Debes aceptar la política de privacidad.");
        }

        if (formulario.Password != formulario.PasswordConfirmar)
        {
            ModelState.AddModelError(nameof(formulario.PasswordConfirmar), "Las contraseñas no coinciden.");
        }

        if (formulario.Rol == "operador" && string.IsNullOrWhiteSpace(formulario.Organizacion))
        {
            ModelState.AddModelError(nameof(formulario.Organizacion), "Indica la organización o comedor.");
        }

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        if (formulario.Rol == "operador")
        {
            var solicitud = await api.SolicitarOperadorAsync(
                new SolicitudOperadorRequest
                {
                    Nombre = formulario.Nombre.Trim(),
                    Correo = formulario.Correo.Trim(),
                    Password = formulario.Password,
                    Organizacion = formulario.Organizacion!.Trim()
                },
                cancellationToken);

            if (!solicitud.Exito)
            {
                formulario.Error = solicitud.Mensaje;
                return View(formulario);
            }

            TempData["Exito"] = "Solicitud enviada. Un administrador revisará tu acceso como Operador.";
            return RedirectToAction(nameof(Login));
        }

        var registro = await api.RegistrarDonanteAsync(
            new RegistroDonanteRequest
            {
                Nombre = formulario.Nombre.Trim(),
                Correo = formulario.Correo.Trim(),
                Password = formulario.Password,
                Organizacion = string.IsNullOrWhiteSpace(formulario.Organizacion)
                    ? null
                    : formulario.Organizacion.Trim()
            },
            cancellationToken);

        if (!registro.Exito)
        {
            formulario.Error = registro.Mensaje;
            return View(formulario);
        }

        var login = await api.LoginAsync(
            new LoginRequest
            {
                Correo = formulario.Correo.Trim(),
                Password = formulario.Password
            },
            cancellationToken);

        if (!login.Exito)
        {
            TempData["Exito"] = "Cuenta creada. Inicia sesión con tus credenciales.";
            return RedirectToAction(nameof(Login));
        }

        await IniciarSesionAsync(login.Valor!, recordarme: false);
        TempData["Exito"] = "Tu cuenta está lista.";
        return RedirectToActionDeRol(login.Valor!.Usuario.Rol);
    }

    [AllowAnonymous]
    public IActionResult RecuperarPassword()
    {
        ViewData["Title"] = "Recuperar acceso";
        return View(new RecuperarPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecuperarPassword(
        RecuperarPasswordViewModel formulario,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Recuperar acceso";

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = await api.RecuperarPasswordAsync(
            new RecuperarPasswordRequest { Correo = formulario.Correo.Trim() },
            cancellationToken);

        if (!resultado.Exito)
        {
            formulario.Error = resultado.Mensaje;
            return View(formulario);
        }

        formulario.Mensaje = resultado.Valor!.Mensaje;
        formulario.EnlaceDesarrollo = resultado.Valor.EnlaceDesarrollo;

        return View(formulario);
    }

    [AllowAnonymous]
    public IActionResult RestablecerPassword(string? token)
    {
        ViewData["Title"] = "Restablecer contraseña";

        if (string.IsNullOrWhiteSpace(token))
        {
            return View(new RestablecerPasswordViewModel
            {
                Error = "El enlace de recuperación no es válido."
            });
        }

        return View(new RestablecerPasswordViewModel { Token = token });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestablecerPassword(
        RestablecerPasswordViewModel formulario,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Restablecer contraseña";

        if (formulario.Password != formulario.PasswordConfirmar)
        {
            ModelState.AddModelError(nameof(formulario.PasswordConfirmar), "Las contraseñas no coinciden.");
        }

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var resultado = await api.RestablecerPasswordAsync(
            new RestablecerPasswordRequest
            {
                Token = formulario.Token,
                Password = formulario.Password
            },
            cancellationToken);

        if (!resultado.Exito)
        {
            formulario.Error = resultado.Mensaje;
            return View(formulario);
        }

        TempData["Exito"] = "Contraseña actualizada. Ya puedes iniciar sesión.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public async Task<IActionResult> MiCuenta(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Mi cuenta";

        var resultado = await api.ObtenerSesionAsync(cancellationToken: cancellationToken);

        return View(new MiCuentaViewModel
        {
            Usuario = resultado.Exito ? resultado.Valor : null,
            Error = resultado.Exito ? null : resultado.Mensaje,
            Exito = TempData["Exito"] as string
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarPassword(
        CambiarPasswordViewModel formulario,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Mi cuenta";

        if (formulario.PasswordNueva != formulario.PasswordConfirmar)
        {
            ModelState.AddModelError(nameof(formulario.PasswordConfirmar), "Las contraseñas no coinciden.");
        }

        if (!ModelState.IsValid)
        {
            return View("MiCuenta", new MiCuentaViewModel
            {
                Usuario = (await api.ObtenerSesionAsync(cancellationToken: cancellationToken)).Valor,
                Error = "Revisa los datos de la contraseña."
            });
        }

        var resultado = await api.CambiarPasswordAsync(
            new CambiarPasswordRequest
            {
                PasswordActual = formulario.PasswordActual,
                PasswordNueva = formulario.PasswordNueva
            },
            cancellationToken);

        if (!resultado.Exito)
        {
            return View("MiCuenta", new MiCuentaViewModel
            {
                Usuario = (await api.ObtenerSesionAsync(cancellationToken: cancellationToken)).Valor,
                Error = resultado.Mensaje
            });
        }

        TempData["Exito"] = "Contraseña actualizada.";
        return RedirectToAction(nameof(MiCuenta));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await api.LogoutAsync(cancellationToken);
        HttpContext.Session.Remove(AuthSessionKeys.Token);
        HttpContext.Session.Remove(AuthSessionKeys.Expiracion);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccesoDenegado()
    {
        ViewData["Title"] = "Acceso denegado";
        return View();
    }

    private async Task IniciarSesionAsync(LoginResponse respuesta, bool recordarme)
    {
        HttpContext.Session.SetString(AuthSessionKeys.Token, respuesta.Token);
        HttpContext.Session.SetString(
            AuthSessionKeys.Expiracion,
            respuesta.ExpiraAt.ToString("O"));

        var propiedades = new AuthenticationProperties
        {
            IsPersistent = recordarme,
            ExpiresUtc = respuesta.ExpiraAt,
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            MvcAuthClaims.Crear(respuesta.Usuario),
            propiedades);
    }

    private RedirectToActionResult RedirectToActionDeRol(string rol) => rol switch
    {
        "Administrador" => RedirectToAction("Index", "AdminUsuarios"),
        "Operador" or "Coordinador" => RedirectToAction("Index", "ControlPanel"),
        "Donante" => RedirectToAction("Index", "FoodBank"),
        _ => RedirectToAction("Index", "Home")
    };
}
