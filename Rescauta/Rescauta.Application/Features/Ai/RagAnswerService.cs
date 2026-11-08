using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Orquesta el motor RAG público.
///
/// Garantías de seguridad y trazabilidad:
///   * no llama a Gemini si no hay contexto relevante,
///   * solo devuelve fuentes incluidas en el contexto y efectivamente citadas,
///   * rechaza citas inventadas o fuera de rango,
///   * devuelve un fallback seguro ante bloqueo, secreto o fuga del prompt,
///   * nunca registra la pregunta, el prompt ni la respuesta completos.
/// </summary>
public sealed class RagAnswerService(
    IValidator<PreguntaChatbotDto> validator,
    IRagRetriever retriever,
    IRagContextBuilder contextBuilder,
    IRagPromptBuilder promptBuilder,
    IGeminiClient geminiClient,
    ChatbotQuestionRouter questionRouter,
    IChatbotPublicDataProvider publicDataProvider,
    ISecretRedactor secretRedactor,
    RagConcurrencyGate concurrencyGate,
    IOptions<AiRagOptions> options,
    ILogger<RagAnswerService> logger) : IRagAnswerService
{
    public const string InsufficientInformationMessage =
        "No encontré información suficiente en la documentación institucional de Rescauta para responder esa pregunta.";

    public const string SafeFallbackMessage =
        "No puedo responder a esa solicitud con la información disponible. Si tienes una pregunta sobre la documentación institucional de Rescauta, puedo ayudarte.";

    private const string InsufficientInformationMarker =
        "no encontre informacion suficiente en la documentacion institucional de rescauta";

    private static readonly Regex CitationGroupRegex = new(
        @"\[([^\[\]]*?S\d+[^\[\]]*?)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CitationNumberRegex = new(
        @"S(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ValidCitationGroupRegex = new(
        @"^S\d+(\s*,\s*S\d+)*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] PromptLeakMarkers =
    [
        "jerarquia de instrucciones",
        "datos no confiables",
        "eres el asistente documental oficial de rescauta",
        "reglas son obligatorias y prevalecen"
    ];

    private static readonly string[] BlockedFinishReasons =
    [
        "SAFETY",
        "RECITATION",
        "BLOCKLIST",
        "PROHIBITED_CONTENT",
        "SPII"
    ];

    public async Task<Result<RespuestaChatbotDto>> PreguntarAsync(
        PreguntaChatbotDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.PreguntaInvalida,
                "La pregunta es obligatoria.");
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            var message = string.Join(
                " ",
                validation.Errors
                    .Select(error => error.ErrorMessage)
                    .Distinct(StringComparer.Ordinal));

            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.PreguntaInvalida,
                message);
        }

        var settings = options.Value;
        var pregunta = request.Pregunta.Trim();
        var started = Stopwatch.StartNew();

        if (questionRouter.IsHybridQuestion(pregunta))
        {
            return await PreguntarHibridoAsync(pregunta, settings, started, cancellationToken);
        }

        var publicQuery = questionRouter.Classify(pregunta);

        if (publicQuery is not null)
        {
            ChatbotDynamicDataDto directory;

            try
            {
                directory = await publicDataProvider.ObtenerDirectorioComedoresAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                logger.LogError("No se pudo consultar el directorio público de comedores.");

                return Result<RespuestaChatbotDto>.Failure(
                    ChatbotErrorCodes.ErrorInterno,
                    "No se pudo consultar la información de comedores en este momento.");
            }

            return Result<RespuestaChatbotDto>.Success(
                BuildPublicComedoresAnswer(publicQuery.Value, directory));
        }

        IReadOnlyList<DocumentSearchResult> fragments;

        try
        {
            fragments = await retriever.RetrieveAsync(pregunta, cancellationToken);
        }
        catch (RagRetrievalException ex)
        {
            var code = MapRetrievalFailure(ex.Failure);

            logger.LogWarning(
                "La recuperación RAG falló con {Failure}. Se devuelve un error controlado.",
                ex.Failure);

            return Result<RespuestaChatbotDto>.Failure(code, MessageFor(code));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("La recuperación RAG excedió el tiempo total disponible.");

            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.AiTimeout,
                MessageFor(ChatbotErrorCodes.AiTimeout));
        }

        if (fragments.Count == 0)
        {
            logger.LogInformation(
                "Sin fragmentos relevantes para la pregunta. No se llamó a Gemini. Duración: {DurationMs} ms.",
                started.ElapsedMilliseconds);

            return Result<RespuestaChatbotDto>.Success(InsufficientAnswer());
        }

        var context = contextBuilder.Build(fragments);

        if (context.Fragments.Count == 0)
        {
            logger.LogInformation(
                "El contexto quedó vacío tras deduplicar y aplicar límites. No se llamó a Gemini. " +
                "Duración: {DurationMs} ms.",
                started.ElapsedMilliseconds);

            return Result<RespuestaChatbotDto>.Success(InsufficientAnswer());
        }

        var prompt = promptBuilder.Build(pregunta, context);
        var waitTime = TimeSpan.FromSeconds(Math.Max(1, settings.ConcurrencyWaitSeconds));
        var acquired = false;

        try
        {
            acquired = await concurrencyGate.WaitAsync(waitTime, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (!acquired)
        {
            logger.LogWarning("Se alcanzó el límite de generaciones concurrentes.");

            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.AiOcupado,
                MessageFor(ChatbotErrorCodes.AiOcupado));
        }

        AiGenerationResult generation;

        try
        {
            using var timeout = CreateTimeout(
                settings.GenerationTimeoutSeconds,
                cancellationToken);

            generation = await geminiClient.GenerateAsync(
                new AiGenerationRequest
                {
                    SystemInstruction = prompt.SystemInstruction,
                    UserPrompt = prompt.UserPrompt,
                    Temperature = Math.Clamp(settings.Temperature, 0d, 2d),
                    MaxOutputTokens = Math.Max(1, settings.MaxOutputTokens)
                },
                timeout.Token);
        }
        catch (AiProviderException ex)
        {
            if (ex.Kind == AiFailureKind.Blocked)
            {
                logger.LogWarning(
                    "Gemini bloqueó la respuesta. Se devuelve el fallback seguro.");

                return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
            }

            var code = MapAiFailure(ex.Kind);

            logger.LogWarning(
                "La generación RAG falló con {Failure}. Se devuelve un error controlado.",
                ex.Kind);

            return Result<RespuestaChatbotDto>.Failure(code, MessageFor(code));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("La generación RAG excedió el timeout configurado.");

            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.AiTimeout,
                MessageFor(ChatbotErrorCodes.AiTimeout));
        }
        finally
        {
            if (acquired)
            {
                concurrencyGate.Release();
            }
        }

        var generatedText = generation.Text.Trim();

        if (generatedText.Length == 0 || IsBlocked(generation.FinishReason))
        {
            logger.LogWarning(
                "Gemini devolvió una respuesta vacía o bloqueada ({FinishReason}). " +
                "Se devuelve el fallback seguro.",
                generation.FinishReason);

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        if (secretRedactor.ContainsSecret(generatedText))
        {
            logger.LogWarning(
                "La respuesta del proveedor contenía un secreto configurado. " +
                "Se devolvió el fallback seguro.");

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        if (ContainsPromptLeak(generatedText) || ContainsUnsafeHtml(generatedText))
        {
            logger.LogWarning(
                "La respuesta del proveedor contenía una fuga del prompt o HTML no seguro. " +
                "Se devolvió el fallback seguro.");

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        if (ContainsInsufficientMarker(generatedText))
        {
            logger.LogInformation(
                "Gemini indicó que no hay información suficiente. Se devuelven fuentes vacías.");

            return Result<RespuestaChatbotDto>.Success(InsufficientAnswer());
        }

        var answer = TruncateAtSentence(
            generatedText,
            Math.Max(100, settings.MaxAnswerCharacters));

        var citedNumbers = ParseCitations(answer, out var citationsAreValid);

        if (!citationsAreValid)
        {
            logger.LogWarning(
                "Gemini generó citas con formato inválido. Se devolvió el fallback seguro.");

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        if (citedNumbers.Any(number => number < 1 || number > context.Fragments.Count))
        {
            logger.LogWarning(
                "Gemini generó citas fuera del contexto. Se devolvió el fallback seguro.");

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        if (settings.RequireCitations && citedNumbers.Count == 0)
        {
            logger.LogWarning(
                "Gemini no citó ninguna fuente del contexto. Se devolvió el fallback seguro.");

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        var sources = BuildSources(context, citedNumbers, settings.RequireCitations);

        if (sources.Count == 0)
        {
            logger.LogWarning(
                "No quedaron fuentes válidas para respaldar la respuesta. " +
                "Se devolvió el fallback seguro.");

            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        logger.LogInformation(
            "Respuesta RAG generada en {DurationMs} ms: {Fragments} fragmentos de contexto, " +
            "{Sources} fuentes citadas, {PromptTokens} tokens de entrada, " +
            "{CandidateTokens} tokens de salida.",
            started.ElapsedMilliseconds,
            context.Fragments.Count,
            sources.Count,
            generation.PromptTokens,
            generation.CandidateTokens);

        return Result<RespuestaChatbotDto>.Success(new RespuestaChatbotDto
        {
            Respuesta = answer,
            SuficienteInformacion = true,
            Fuentes = sources
        });
    }

    private async Task<Result<RespuestaChatbotDto>> PreguntarHibridoAsync(
        string pregunta,
        AiRagOptions settings,
        Stopwatch started,
        CancellationToken cancellationToken)
    {
        // The two independent, read-only sources are queried concurrently. Each
        // helper converts provider failures to a safe status; caller cancellation
        // is always rethrown.
        var retrievalTask = RetrieveHybridContextAsync(pregunta, cancellationToken);
        var publicDataTask = ReadHybridPublicDataAsync(cancellationToken);
        await Task.WhenAll(retrievalTask, publicDataTask);

        var retrieval = await retrievalTask;
        var publicData = await publicDataTask;

        if (retrieval.ErrorCode is not null && publicData.ErrorCode is not null)
        {
            var failureCode = retrieval.ErrorCode == ChatbotErrorCodes.AiTimeout ||
                              publicData.ErrorCode == ChatbotErrorCodes.AiTimeout
                ? ChatbotErrorCodes.AiTimeout
                : retrieval.ErrorCode;

            return Result<RespuestaChatbotDto>.Failure(failureCode, MessageFor(failureCode));
        }

        var context = retrieval.Context ?? contextBuilder.Empty;
        var documentsUnavailable = retrieval.ErrorCode is not null;
        var documentsInsufficient = !documentsUnavailable && context.Fragments.Count == 0;
        var isPartial = documentsUnavailable || documentsInsufficient || publicData.ErrorCode is not null;
        if (promptBuilder is not RagPromptBuilder hybridPromptBuilder)
        {
            logger.LogError("No está disponible el constructor de prompts híbridos.");
            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.ErrorInterno,
                "No se pudo procesar la consulta en este momento.");
        }

        var prompt = hybridPromptBuilder.BuildHybrid(
            pregunta,
            context,
            publicData.Data,
            documentsUnavailable,
            publicData.ErrorCode is not null,
            documentsInsufficient);

        var waitTime = TimeSpan.FromSeconds(Math.Max(1, settings.ConcurrencyWaitSeconds));
        var acquired = false;

        try
        {
            acquired = await concurrencyGate.WaitAsync(waitTime, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (!acquired)
        {
            logger.LogWarning("Se alcanzó el límite de generaciones concurrentes.");
            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.AiOcupado,
                MessageFor(ChatbotErrorCodes.AiOcupado));
        }

        AiGenerationResult generation;
        try
        {
            using var timeout = CreateTimeout(settings.GenerationTimeoutSeconds, cancellationToken);
            generation = await geminiClient.GenerateAsync(
                new AiGenerationRequest
                {
                    SystemInstruction = prompt.SystemInstruction,
                    UserPrompt = prompt.UserPrompt,
                    Temperature = Math.Clamp(settings.Temperature, 0d, 2d),
                    MaxOutputTokens = Math.Max(1, settings.MaxOutputTokens)
                },
                timeout.Token);
        }
        catch (AiProviderException ex)
        {
            if (ex.Kind == AiFailureKind.Blocked)
            {
                return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
            }

            var code = MapAiFailure(ex.Kind);
            return Result<RespuestaChatbotDto>.Failure(code, MessageFor(code));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<RespuestaChatbotDto>.Failure(
                ChatbotErrorCodes.AiTimeout,
                MessageFor(ChatbotErrorCodes.AiTimeout));
        }
        finally
        {
            if (acquired)
            {
                concurrencyGate.Release();
            }
        }

        var generatedText = generation.Text.Trim();
        if (generatedText.Length == 0 ||
            IsBlocked(generation.FinishReason) ||
            secretRedactor.ContainsSecret(generatedText) ||
            ContainsPromptLeak(generatedText) ||
            ContainsUnsafeHtml(generatedText) ||
            ContainsInsufficientMarker(generatedText))
        {
            logger.LogWarning("La generación híbrida no superó las validaciones de seguridad o suficiencia.");
            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        var answer = TruncateAtSentence(
            generatedText,
            Math.Max(100, settings.MaxAnswerCharacters));
        var citationValidation = HybridCitationValidator.Validate(
            answer,
            context,
            publicData.Data);
        if (!citationValidation.IsValid)
        {
            logger.LogWarning(
                "Se rechazó la respuesta híbrida por validación de citas: {Reason}.",
                citationValidation.RejectionReason);
            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        var sources = BuildHybridSources(context, publicData.Data, citationValidation.CitationIds);
        if (sources.Count == 0)
        {
            return Result<RespuestaChatbotDto>.Success(SafeFallbackAnswer());
        }

        if (isPartial)
        {
            answer = $"{answer} La respuesta es parcial: una parte de la consulta no pudo verificarse con las fuentes disponibles.";
        }

        logger.LogInformation(
            "Respuesta híbrida generada en {DurationMs} ms: {DocumentSources} fuentes documentales, " +
            "{PublicDataAvailable} fuente(s) pública(s), {Partial} parcial.",
            started.ElapsedMilliseconds,
            context.Fragments.Count,
            publicData.Data is null ? 0 : 1,
            isPartial);

        return Result<RespuestaChatbotDto>.Success(new RespuestaChatbotDto
        {
            Respuesta = answer,
            SuficienteInformacion = !isPartial,
            Fuentes = sources
        });
    }

    private async Task<(RagContext? Context, string? ErrorCode)> RetrieveHybridContextAsync(
        string pregunta,
        CancellationToken cancellationToken)
    {
        try
        {
            var fragments = await retriever.RetrieveAsync(pregunta, cancellationToken);
            return (contextBuilder.Build(fragments), null);
        }
        catch (RagRetrievalException ex)
        {
            var code = MapRetrievalFailure(ex.Failure);
            logger.LogWarning("Falló la fuente documental híbrida con {Failure}.", ex.Failure);
            return (null, code);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("La consulta documental híbrida excedió el tiempo disponible.");
            return (null, ChatbotErrorCodes.AiTimeout);
        }
        catch (Exception)
        {
            logger.LogWarning("No se pudo consultar la fuente documental híbrida.");
            return (null, ChatbotErrorCodes.AiNoDisponible);
        }
    }

    private async Task<(ChatbotDynamicDataDto? Data, string? ErrorCode)> ReadHybridPublicDataAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await publicDataProvider.ObtenerDirectorioComedoresAsync(cancellationToken);
            if (data is null)
            {
                logger.LogWarning("La consulta híbrida del directorio público no devolvió datos.");
                return (null, ChatbotErrorCodes.ErrorInterno);
            }

            return (data, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("La consulta híbrida del directorio público excedió el tiempo disponible.");
            return (null, ChatbotErrorCodes.AiTimeout);
        }
        catch (Exception)
        {
            logger.LogWarning("No se pudo consultar el directorio público para la pregunta híbrida.");
            return (null, ChatbotErrorCodes.ErrorInterno);
        }
    }

    private static IReadOnlyList<FuenteChatbotDto> BuildHybridSources(
        RagContext context,
        ChatbotDynamicDataDto? publicData,
        IReadOnlySet<string> citations)
    {
        var sources = context.Fragments
            .Where(fragment => citations.Contains(fragment.CitationId))
            .OrderBy(fragment => GetCitationNumber(fragment.CitationId))
            .Select(fragment => new FuenteChatbotDto
            {
                Id = fragment.CitationId,
                Titulo = fragment.Title,
                Fuente = fragment.Source,
                Categoria = fragment.Category,
                Relevancia = Math.Round(fragment.Score, 3)
            })
            .ToList();

        if (publicData is not null && citations.Contains("D1"))
        {
            sources.Add(new FuenteChatbotDto
            {
                Id = "D1",
                Titulo = "Directorio público de comedores de Rescauta",
                Fuente = "Registro público de comedores",
                Categoria = "datos_actuales",
                Relevancia = 1d
            });
        }

        return sources;
    }

    private static RespuestaChatbotDto InsufficientAnswer() => new()
    {
        Respuesta = InsufficientInformationMessage,
        SuficienteInformacion = false,
        Fuentes = Array.Empty<FuenteChatbotDto>()
    };

    private static RespuestaChatbotDto BuildPublicComedoresAnswer(
        ChatbotPublicQueryKind queryKind,
        ChatbotDynamicDataDto directory)
    {
        var answer = directory.TotalComedores == 0
            ? "Actualmente no hay comedores registrados."
            : queryKind switch
            {
                ChatbotPublicQueryKind.CountComedores =>
                    $"Hay {directory.TotalComedores} {PluralizeComedores(directory.TotalComedores)} " +
                    $"{(directory.TotalComedores == 1 ? "registrado" : "registrados")}.",
                ChatbotPublicQueryKind.DistrictsWithComedores =>
                    $"Hay comedores registrados en estos distritos: {string.Join(", ", directory.Distritos)}.",
                _ => BuildComedoresList(directory)
            };

        return new RespuestaChatbotDto
        {
            Respuesta = answer,
            SuficienteInformacion = true,
            Fuentes =
            [
                new FuenteChatbotDto
                {
                    Id = "directorio-publico-comedores",
                    Titulo = "Directorio público de comedores de Rescauta",
                    Fuente = "Registro público de comedores",
                    Categoria = "datos_actuales",
                    Relevancia = 1d
                }
            ]
        };
    }

    private static string BuildComedoresList(ChatbotDynamicDataDto directory)
    {
        var comedores = directory.Comedores
            .Take(ChatbotPublicDataProvider.MaxComedores)
            .ToArray();

        if (comedores.Length == 0)
        {
            return "No hay comedores registrados para mostrar.";
        }

        var rows = string.Join(
            "; ",
            comedores.Select(comedor => $"{comedor.Nombre} ({comedor.Distrito})"));

        var response = $"Comedores registrados: {rows}.";

        return directory.TotalComedores > comedores.Length
            ? $"{response} Se muestran {comedores.Length} de {directory.TotalComedores}."
            : response;
    }

    private static string PluralizeComedores(int total)
        => total == 1 ? "comedor" : "comedores";

    private static RespuestaChatbotDto SafeFallbackAnswer() => new()
    {
        Respuesta = SafeFallbackMessage,
        SuficienteInformacion = false,
        Fuentes = Array.Empty<FuenteChatbotDto>()
    };

    private static IReadOnlyList<FuenteChatbotDto> BuildSources(
        RagContext context,
        HashSet<int> citedNumbers,
        bool requireCitations)
    {
        var selected = context.Fragments
            .Where(fragment =>
            {
                if (!requireCitations && citedNumbers.Count == 0)
                {
                    return true;
                }

                return citedNumbers.Contains(GetCitationNumber(fragment.CitationId));
            })
            .OrderBy(fragment => GetCitationNumber(fragment.CitationId))
            .Select(fragment => new FuenteChatbotDto
            {
                Id = fragment.CitationId,
                Titulo = fragment.Title,
                Fuente = fragment.Source,
                Categoria = fragment.Category,
                Relevancia = Math.Round(fragment.Score, 3)
            })
            .ToArray();

        return selected;
    }

    private static HashSet<int> ParseCitations(
        string text,
        out bool citationsAreValid)
    {
        var numbers = new HashSet<int>();
        citationsAreValid = true;

        // Acepta citas individuales [S1] y agrupadas [S1, S3]. Cualquier corchete
        // que contenga una referencia y no sea una lista limpia de S# se considera
        // inválido: no se puede validar ni devolver una fuente en ese formato.
        foreach (Match group in CitationGroupRegex.Matches(text))
        {
            var content = group.Groups[1].Value.Trim();

            if (!ValidCitationGroupRegex.IsMatch(content))
            {
                citationsAreValid = false;
                numbers.Clear();
                return numbers;
            }

            foreach (Match numberMatch in CitationNumberRegex.Matches(content))
            {
                if (int.TryParse(
                        numberMatch.Groups[1].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var number))
                {
                    numbers.Add(number);
                }
            }
        }

        return numbers;
    }

    private static int GetCitationNumber(string citationId)
        => citationId.Length > 1 &&
           int.TryParse(
               citationId.AsSpan(1),
               NumberStyles.None,
               CultureInfo.InvariantCulture,
               out var number)
            ? number
            : 0;

    private static bool IsBlocked(string finishReason)
        => BlockedFinishReasons.Contains(
            finishReason,
            StringComparer.OrdinalIgnoreCase);

    private static bool ContainsInsufficientMarker(string text)
        => NormalizeForMatching(text).Contains(
            InsufficientInformationMarker,
            StringComparison.Ordinal);

    private static bool ContainsPromptLeak(string text)
    {
        var normalized = NormalizeForMatching(text);

        return PromptLeakMarkers.Any(marker =>
            normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static bool ContainsUnsafeHtml(string text)
        => text.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("<iframe", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("javascript:", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeForMatching(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static string TruncateAtSentence(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var cut = value[..maxLength];
        var sentenceEnd = cut.LastIndexOfAny(['.', '!', '?', '\n']);

        if (sentenceEnd > maxLength / 2)
        {
            return cut[..(sentenceEnd + 1)].TrimEnd();
        }

        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > 0 ? cut[..lastSpace] : cut).TrimEnd();
    }

    private static CancellationTokenSource CreateTimeout(
        int seconds,
        CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, seconds)));
        return timeout;
    }

    private static string MapRetrievalFailure(RagRetrievalFailure failure) => failure switch
    {
        RagRetrievalFailure.AiNotConfigured => ChatbotErrorCodes.AiNoConfigurada,
        RagRetrievalFailure.AiTimeout => ChatbotErrorCodes.AiTimeout,
        RagRetrievalFailure.AiInvalidResponse => ChatbotErrorCodes.AiRespuestaInvalida,
        RagRetrievalFailure.QdrantUnavailable => ChatbotErrorCodes.QdrantNoDisponible,
        RagRetrievalFailure.QdrantTimeout => ChatbotErrorCodes.QdrantTimeout,
        _ => ChatbotErrorCodes.AiNoDisponible
    };

    private static string MapAiFailure(AiFailureKind kind) => kind switch
    {
        AiFailureKind.NotConfigured => ChatbotErrorCodes.AiNoConfigurada,
        AiFailureKind.Timeout => ChatbotErrorCodes.AiTimeout,
        AiFailureKind.InvalidResponse => ChatbotErrorCodes.AiRespuestaInvalida,
        _ => ChatbotErrorCodes.AiNoDisponible
    };

    private static string MessageFor(string code) => code switch
    {
        ChatbotErrorCodes.AiNoConfigurada =>
            "El asistente no está disponible en este momento.",
        ChatbotErrorCodes.AiTimeout =>
            "El asistente tardó demasiado en responder. Intenta nuevamente.",
        ChatbotErrorCodes.AiRespuestaInvalida =>
            "El asistente no pudo generar una respuesta válida.",
        ChatbotErrorCodes.AiOcupado =>
            "El asistente está recibiendo muchas solicitudes. Intenta nuevamente en unos segundos.",
        ChatbotErrorCodes.QdrantNoDisponible =>
            "El asistente no está disponible en este momento.",
        ChatbotErrorCodes.QdrantTimeout =>
            "El asistente tardó demasiado en responder. Intenta nuevamente.",
        _ => "El asistente no está disponible en este momento."
    };
}
