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
