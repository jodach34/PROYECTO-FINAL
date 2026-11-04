using FluentValidation;
using Rescauta.Application.Features.Kardex.Dto;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Application.Features.Kardex.Validators;

public sealed class CrearInsumoValidator : AbstractValidator<CrearInsumoDto>
{
    public CrearInsumoValidator()
    {
        RuleFor(x => x.ComedorId)
            .NotEmpty()
            .WithMessage("El comedor es obligatorio.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre del insumo es obligatorio.")
            .MaximumLength(Insumo.NombreMaxLength)
            .WithMessage($"El nombre no puede superar los {Insumo.NombreMaxLength} caracteres.");

        RuleFor(x => x.UnidadMedida)
            .NotEmpty()
            .WithMessage("La unidad de medida es obligatoria.")
            .MaximumLength(Insumo.UnidadMedidaMaxLength)
            .WithMessage($"La unidad de medida no puede superar los {Insumo.UnidadMedidaMaxLength} caracteres.");

        RuleFor(x => x.StockInicial)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("El stock inicial no puede ser negativo.");

        RuleFor(x => x.StockMaximo)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("El stock maximo no puede ser negativo.");
    }
}
