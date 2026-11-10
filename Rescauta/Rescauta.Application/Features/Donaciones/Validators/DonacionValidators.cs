using FluentValidation;
using Rescauta.Application.Features.Donaciones.Dto;
using Rescauta.Domain.Entities.Donaciones;

namespace Rescauta.Application.Features.Donaciones.Validators;

public sealed class RegistrarDonacionValidator : AbstractValidator<RegistrarDonacionDto>
{
    public RegistrarDonacionValidator()
    {
        RuleFor(x => x.InsumoId)
            .NotEmpty()
            .WithMessage("El insumo a donar es obligatorio.");

        RuleFor(x => x.Cantidad)
            .GreaterThan(0)
            .WithMessage("La cantidad debe ser mayor que cero.");

        RuleFor(x => x.Donante)
            .NotEmpty()
            .WithMessage("El nombre del donante es obligatorio.")
            .MaximumLength(Donacion.DonanteMaxLength)
            .WithMessage($"El nombre del donante no puede superar {Donacion.DonanteMaxLength} caracteres.");

        RuleFor(x => x.PuntoRecojo)
            .MaximumLength(Donacion.PuntoRecojoMaxLength)
            .WithMessage($"El punto de recojo no puede superar {Donacion.PuntoRecojoMaxLength} caracteres.");

        RuleFor(x => x.LatitudOrigen)
            .InclusiveBetween(-90m, 90m)
            .When(x => x.LatitudOrigen.HasValue)
            .WithMessage("La latitud del origen debe estar entre -90 y 90.");

        RuleFor(x => x.LongitudOrigen)
            .InclusiveBetween(-180m, 180m)
            .When(x => x.LongitudOrigen.HasValue)
            .WithMessage("La longitud del origen debe estar entre -180 y 180.");

        RuleFor(x => x)
            .Must(x => x.LatitudOrigen.HasValue == x.LongitudOrigen.HasValue)
            .WithMessage("Debe indicar la latitud y la longitud del origen juntas.");

        RuleFor(x => x)
            .Must(x => !(x.LatitudOrigen == 0m && x.LongitudOrigen == 0m))
            .WithMessage("El origen no puede estar en la coordenada (0, 0).");
    }
}

public sealed class MotivoDonacionValidator : AbstractValidator<MotivoDonacionDto>
{
    public MotivoDonacionValidator()
    {
        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("Indicar el motivo.")
            .MaximumLength(Donacion.MotivoCierreMaxLength)
            .WithMessage($"El motivo no puede superar {Donacion.MotivoCierreMaxLength} caracteres.");
    }
}
