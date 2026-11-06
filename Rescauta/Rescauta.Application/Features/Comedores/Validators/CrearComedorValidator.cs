using FluentValidation;
using Rescauta.Application.Features.Comedores.Dto;
using Rescauta.Domain.Entities.Comedores;

namespace Rescauta.Application.Features.Comedores.Validators;

public sealed class CrearComedorValidator : AbstractValidator<CrearComedorDto>
{
    public CrearComedorValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre del comedor es obligatorio.")
            .MaximumLength(Comedor.NombreMaxLength)
            .WithMessage($"El nombre del comedor no puede superar los {Comedor.NombreMaxLength} caracteres.");

        RuleFor(x => x.Distrito)
            .NotEmpty()
            .WithMessage("El distrito del comedor es obligatorio.")
            .MaximumLength(Comedor.DistritoMaxLength)
            .WithMessage($"El distrito no puede superar los {Comedor.DistritoMaxLength} caracteres.");

        RuleFor(x => x.RacionesDiarias)
            .GreaterThan(0)
            .WithMessage("Las raciones diarias deben ser mayores que cero.");

        RuleFor(x => x.Direccion)
            .NotEmpty()
            .WithMessage("La direccion del comedor es obligatoria.")
            .MaximumLength(Comedor.DireccionMaxLength)
            .WithMessage($"La direccion no puede superar los {Comedor.DireccionMaxLength} caracteres.");

        RuleFor(x => x.ContactoTelefono)
            .MaximumLength(Comedor.ContactoTelefonoMaxLength)
            .WithMessage($"El telefono de contacto no puede superar los {Comedor.ContactoTelefonoMaxLength} caracteres.");

        RuleFor(x => x.MapaX)
            .InclusiveBetween(0m, 100m)
            .WithMessage("La posicion horizontal del mapa debe estar entre 0 y 100.");

        RuleFor(x => x.MapaY)
            .InclusiveBetween(0m, 100m)
            .WithMessage("La posicion vertical del mapa debe estar entre 0 y 100.");
    }
}
