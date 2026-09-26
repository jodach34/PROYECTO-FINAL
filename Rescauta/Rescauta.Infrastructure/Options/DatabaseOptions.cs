namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "Database" de appsettings.json:
///   "Database": { "Provider": "Sqlite", "ConnectionString": "Data Source=rescauta.db" }
///
/// Provider valido: "Sqlite" (default, desarrollo), "SqlServer", "Postgres".
/// Cambiar de motor es cambiar una linea de configuracion, no una linea de codigo.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "Sqlite";

    public string ConnectionString { get; set; } = string.Empty;

    public bool EnableSensitiveDataLogging { get; set; }

    public bool MigrateOnStartup { get; set; }
}
