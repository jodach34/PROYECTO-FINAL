using System.ComponentModel.DataAnnotations;

namespace PROYECTO_FINAL.Models.Api;

/// <summary>Pregunta que recibe el proxy MVC desde el navegador.</summary>
public sealed class ChatbotProxyRequest
{
    [Required(ErrorMessage = "La pregunta es obligatoria.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "La pregunta debe tener entre 3 y 500 caracteres.")]
    public string Pregunta { get; set; } = string.Empty;
}

/// <summary>Respuesta pública del chatbot que el proxy entrega al navegador.</summary>
public sealed class ChatbotProxyResponse
{
    public string Respuesta { get; set; } = string.Empty;

    public bool SuficienteInformacion { get; set; }

    public IReadOnlyList<ChatbotProxySource> Fuentes { get; set; } = [];
}

/// <summary>Fuente documental citada por el chatbot.</summary>
public sealed class ChatbotProxySource
{
    public string Id { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string Fuente { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    public double Relevancia { get; set; }
}

/// <summary>Respuesta de error segura del proxy MVC.</summary>
public sealed class ChatbotProxyError
{
    public string Codigo { get; init; } = string.Empty;

    public string Mensaje { get; init; } = string.Empty;
}
