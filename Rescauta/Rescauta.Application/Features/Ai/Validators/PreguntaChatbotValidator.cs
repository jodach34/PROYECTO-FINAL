using FluentValidation;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Features.Ai.Validators;

/// <summary>
/// Valida la entrada pública antes de gastar cuota de embeddings o generación.
/// Se ejecuta explícitamente desde el caso de uso, igual que los validadores de los
/// módulos existentes.
/// </summary>
public sealed class PreguntaChatbotValidator : AbstractValidator<PreguntaChatbotDto>
{
    public PreguntaChatbotValidator(IOptions<AiRagOptions> options)
    {
        var settings = options.Value;
        var minLength = Math.Max(1, settings.MinQuestionLength);
        var maxLength = Math.Max(minLength, settings.MaxQuestionLength);

        RuleFor(request => request.Pregunta)
            .NotEmpty()
            .WithMessage("La pregunta es obligatoria.")
            .Must(NoTieneSoloEspacios)
            .WithMessage("La pregunta no puede estar vacía.")
            .Must(value => value.Trim().Length >= minLength)
            .WithMessage($"La pregunta debe tener al menos {minLength} caracteres.")
            .Must(value => value.Trim().Length <= maxLength)
            .WithMessage($"La pregunta no puede superar {maxLength} caracteres.")
            .Must(NoTieneCaracteresDeControl)
            .WithMessage("La pregunta contiene caracteres no permitidos.");
    }

    private static bool NoTieneSoloEspacios(string value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool NoTieneCaracteresDeControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character) &&
                character is not ('\r' or '\n' or '\t'))
            {
                return false;
            }
        }

        return true;
    }
}
