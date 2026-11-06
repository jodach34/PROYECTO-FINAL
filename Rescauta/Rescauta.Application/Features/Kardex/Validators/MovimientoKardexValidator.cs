using FluentValidation;
using Rescauta.Application.Features.Kardex.Dto;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Application.Features.Kardex.Validators;

public sealed class MovimientoKardexValidator : AbstractValidator<MovimientoKardexDto>
{
    public MovimientoKardexValidator()
    {
        RuleFor(x => x.InsumoId)
            .NotEmpty()
            .WithMessage("El insumo es obligatorio.");

        RuleFor(x => x.TipoMovimiento)
            .IsInEnum()
            .WithMessage("El tipo de movimiento no es valido.");

        RuleFor(x => x.Cantidad)
            .GreaterThan(0m)
            .WithMessage("La cantidad debe ser mayor que cero.");

        RuleFor(x => x.Responsable)
            .NotEmpty()
            .WithMessage("El responsable es obligatorio.")
            .MaximumLength(MovimientoKardex.ResponsableMaxLength)
            .WithMessage($"El responsable no puede superar los {MovimientoKardex.ResponsableMaxLength} caracteres.");

        RuleFor(x => x.Concepto)
            .NotEmpty()
            .WithMessage("El concepto es obligatorio.")
            .MaximumLength(MovimientoKardex.ConceptoMaxLength)
            .WithMessage($"El concepto no puede superar los {MovimientoKardex.ConceptoMaxLength} caracteres.");
    }
}
