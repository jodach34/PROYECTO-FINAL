namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "Qdrant" de appsettings.json.
///
/// La ApiKey es un secreto y NUNCA se guarda en appsettings.json. Se toma de
/// user-secrets en local o de la variable de entorno Qdrant__ApiKey en Render.
/// En una instancia local de Qdrant sin autenticacion la clave puede quedar vacia.
/// </summary>
public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    /// <summary>URL base del cluster, por ejemplo https://xxxx.cloud.qdrant.io.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Clave de API de Qdrant Cloud. Vacia en appsettings.json a proposito.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Nombre de la coleccion donde viviran los vectores del RAG.</summary>
    public string Collection { get; set; } = "rescauta_documents";

    /// <summary>Dimension de los vectores. Debe coincidir con la del modelo de embeddings.</summary>
    public int VectorSize { get; set; } = 3072;

    /// <summary>Timeout de las llamadas HTTP a Qdrant, en segundos.</summary>
    public int TimeoutSeconds { get; set; } = 20;
}
