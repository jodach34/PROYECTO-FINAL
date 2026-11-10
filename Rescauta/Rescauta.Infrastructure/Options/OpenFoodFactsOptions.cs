namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "OpenFoodFacts" de appsettings.json. La URL y el timeout se
/// pueden cambiar sin recompilar; no hay claves ni secretos.
/// </summary>
public sealed class OpenFoodFactsOptions
{
    public const string SectionName = "OpenFoodFacts";

    public string BaseUrl { get; set; } = "https://world.openfoodfacts.org/";

    public int TimeoutSeconds { get; set; } = 8;
}
