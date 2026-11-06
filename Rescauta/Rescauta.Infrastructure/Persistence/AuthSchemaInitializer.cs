using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Rescauta.Infrastructure.Persistence;

public static class AuthSchemaInitializer
{
    public static async Task AsegurarAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlite())
        {
            logger.LogWarning(
                "El esquema de autenticacion incremental solo se aplica sobre SQLite. " +
                "En otros proveedores genera y aplica una migracion de EF Core.");
            return;
        }

        foreach (var sql in Sentencias)
        {
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        if (!await ExisteColumnaAsync(context, "donaciones", "UsuarioId", cancellationToken))
        {
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"donaciones\" ADD COLUMN \"UsuarioId\" TEXT NULL;",
                cancellationToken);
        }

        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"ix_donaciones_usuario\" ON \"donaciones\" (\"UsuarioId\");",
            cancellationToken);
    }

    private static async Task<bool> ExisteColumnaAsync(
        AppDbContext context,
        string tabla,
        string columna,
        CancellationToken cancellationToken)
    {
        var conexion = context.Database.GetDbConnection();

        if (conexion.State != ConnectionState.Open)
        {
            await conexion.OpenAsync(cancellationToken);
        }

        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"PRAGMA table_info(\"{tabla}\");";

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        while (await lector.ReadAsync(cancellationToken))
        {
            if (string.Equals(lector.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] Sentencias =
    [
        """
        CREATE TABLE IF NOT EXISTS "usuarios" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_usuarios" PRIMARY KEY,
            "Nombre" TEXT NOT NULL,
            "Correo" TEXT NOT NULL,
            "PasswordHash" TEXT NOT NULL,
            "Rol" TEXT NOT NULL,
            "Estado" TEXT NOT NULL,
            "Organizacion" TEXT NULL,
            "UltimoAcceso" TEXT NULL,
            "IntentosFallidos" INTEGER NOT NULL DEFAULT 0,
            "BloqueadoHasta" TEXT NULL,
            "MotivoRechazo" TEXT NULL,
            "AprobadoPor" TEXT NULL,
            "FechaAprobacion" TEXT NULL,
            "RowVersion" BLOB NULL,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "UpdatedBy" TEXT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "ux_usuarios_correo" ON "usuarios" ("Correo");
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_usuarios_rol" ON "usuarios" ("Rol");
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_usuarios_estado" ON "usuarios" ("Estado");
        """,
        """
        CREATE TABLE IF NOT EXISTS "solicitudes_operador" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_solicitudes_operador" PRIMARY KEY,
            "UsuarioId" TEXT NOT NULL,
            "Organizacion" TEXT NOT NULL,
            "ComedorSolicitadoId" TEXT NULL,
            "Mensaje" TEXT NULL,
            "Estado" TEXT NOT NULL,
            "RevisadoPor" TEXT NULL,
            "FechaRevision" TEXT NULL,
            "MotivoRechazo" TEXT NULL,
            "RowVersion" BLOB NULL,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "UpdatedBy" TEXT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_solicitudes_operador_usuario" ON "solicitudes_operador" ("UsuarioId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_solicitudes_operador_estado" ON "solicitudes_operador" ("Estado");
        """,
        """
        CREATE TABLE IF NOT EXISTS "asignaciones_comedor" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_asignaciones_comedor" PRIMARY KEY,
            "UsuarioId" TEXT NOT NULL,
            "ComedorId" TEXT NOT NULL,
            "Activa" INTEGER NOT NULL DEFAULT 1,
            "RowVersion" BLOB NULL,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "UpdatedBy" TEXT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_asignaciones_comedor" ON "asignaciones_comedor" ("ComedorId");
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "ux_asignaciones_usuario_comedor" ON "asignaciones_comedor" ("UsuarioId", "ComedorId");
        """,
        """
        CREATE TABLE IF NOT EXISTS "sesiones_usuario" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_sesiones_usuario" PRIMARY KEY,
            "UsuarioId" TEXT NOT NULL,
            "TokenHash" TEXT NOT NULL,
            "ExpiraAt" TEXT NOT NULL,
            "RevocadaAt" TEXT NULL,
            "UltimoUsoAt" TEXT NULL,
            "Ip" TEXT NULL,
            "UserAgent" TEXT NULL,
            "RowVersion" BLOB NULL,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "UpdatedBy" TEXT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT "FK_sesiones_usuario_usuarios_UsuarioId"
                FOREIGN KEY ("UsuarioId") REFERENCES "usuarios" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_sesiones_usuario_usuario" ON "sesiones_usuario" ("UsuarioId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_sesiones_usuario_expira" ON "sesiones_usuario" ("ExpiraAt");
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "ux_sesiones_usuario_token" ON "sesiones_usuario" ("TokenHash");
        """,
        """
        CREATE TABLE IF NOT EXISTS "tokens_recuperacion" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_tokens_recuperacion" PRIMARY KEY,
            "UsuarioId" TEXT NOT NULL,
            "TokenHash" TEXT NOT NULL,
            "ExpiraAt" TEXT NOT NULL,
            "UsadoAt" TEXT NULL,
            "RowVersion" BLOB NULL,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "UpdatedBy" TEXT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT "FK_tokens_recuperacion_usuarios_UsuarioId"
                FOREIGN KEY ("UsuarioId") REFERENCES "usuarios" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_tokens_recuperacion_usuario" ON "tokens_recuperacion" ("UsuarioId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "ix_tokens_recuperacion_expira" ON "tokens_recuperacion" ("ExpiraAt");
        """,
        """
        CREATE UNIQUE INDEX IF NOT EXISTS "ux_tokens_recuperacion_token" ON "tokens_recuperacion" ("TokenHash");
        """
    ];
}
