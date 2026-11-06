using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Usuarios.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Usuarios;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Usuarios;

public sealed class UsuarioAdminService(IAppDbContext dbContext) : IUsuarioAdminService
{
    public async Task<Result<IReadOnlyList<UsuarioAdminDto>>> ListarUsuariosAsync(
        string? busqueda = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<Usuario>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Nombre.ToLower().Contains(termino) ||
                u.Correo.Contains(termino));
        }

        var usuarios = await query.ToListAsync(cancellationToken);
        var usuariosOrdenados = usuarios
            .OrderBy(u => u.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<UsuarioAdminDto>>.Success(
            await MapearUsuariosAsync(usuariosOrdenados, cancellationToken));
    }

    public async Task<Result<UsuarioAdminDto>> ObtenerUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await dbContext
            .Set<Usuario>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result<UsuarioAdminDto>.Failure(
                UsuarioAdminErrorCodes.NoEncontrado,
                "El usuario no existe.");
        }

        var mapeado = await MapearUsuariosAsync([usuario], cancellationToken);
        return Result<UsuarioAdminDto>.Success(mapeado[0]);
    }

    public async Task<Result<IReadOnlyList<SolicitudOperadorAdminDto>>> ListarSolicitudesPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        var filas = await (
            from solicitud in dbContext.Set<SolicitudOperador>().AsNoTracking()
            join usuario in dbContext.Set<Usuario>().AsNoTracking()
                on solicitud.UsuarioId equals usuario.Id
            where solicitud.Estado == EstadoSolicitud.Pendiente &&
                  usuario.Estado == EstadoUsuario.Pendiente
            select new
            {
                solicitud.Id,
                solicitud.UsuarioId,
                usuario.Nombre,
                usuario.Correo,
                solicitud.Organizacion,
                solicitud.ComedorSolicitadoId,
                solicitud.Mensaje,
                solicitud.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var comedores = await dbContext
            .Set<Comedor>()
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken);

        var resultado = filas
            .OrderBy(f => f.CreatedAt)
            .Select(f => new SolicitudOperadorAdminDto
            {
                SolicitudId = f.Id,
                UsuarioId = f.UsuarioId,
                Nombre = f.Nombre,
                Correo = f.Correo,
                Organizacion = f.Organizacion,
                ComedorSolicitadoId = f.ComedorSolicitadoId,
                ComedorSolicitadoNombre = f.ComedorSolicitadoId is { } id && comedores.TryGetValue(id, out var nombre)
                    ? nombre
                    : null,
                Mensaje = f.Mensaje,
                FechaSolicitud = f.CreatedAt
            })
            .ToList();

        return Result<IReadOnlyList<SolicitudOperadorAdminDto>>.Success(resultado);
    }

    public async Task<Result> AprobarOperadorAsync(
        Guid usuarioId,
        Guid administradorId,
        AprobarOperadorDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.NoEncontrado, "El usuario no existe.");
        }

        var pendiente = await dbContext
            .Set<SolicitudOperador>()
            .FirstOrDefaultAsync(
                s => s.UsuarioId == usuarioId && s.Estado == EstadoSolicitud.Pendiente,
                cancellationToken);

        if (pendiente is null)
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.SolicitudNoEncontrada,
                "No hay una solicitud pendiente para este usuario.");
        }

        var comedorIds = solicitud.ComedorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (comedorIds.Count == 0 && pendiente.ComedorSolicitadoId is { } solicitado)
        {
            comedorIds.Add(solicitado);
        }

        if (comedorIds.Count == 0)
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.AsignacionInvalida,
                "Asigna al menos un comedor antes de aprobar.");
        }

        if (usuario.Rol == RolUsuario.Operador && comedorIds.Count > 1)
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.AsignacionInvalida,
                "Un Operador solo puede tener un comedor asignado.");
        }

        var validacion = await ValidarComedoresAsync(comedorIds, cancellationToken);
        if (validacion is not null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.AsignacionInvalida, validacion);
        }

        try
        {
            usuario.Aprobar(administradorId, DateTimeOffset.UtcNow);
            pendiente.Aprobar(administradorId, DateTimeOffset.UtcNow);
            await ReemplazarAsignacionesAsync(usuarioId, comedorIds, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(UsuarioAdminErrorCodes.Validacion, ex.Message);
        }
    }

    public async Task<Result> RechazarSolicitudAsync(
        Guid usuarioId,
        Guid administradorId,
        RechazarSolicitudDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.NoEncontrado, "El usuario no existe.");
        }

        var pendiente = await dbContext
            .Set<SolicitudOperador>()
            .FirstOrDefaultAsync(
                s => s.UsuarioId == usuarioId && s.Estado == EstadoSolicitud.Pendiente,
                cancellationToken);

        if (pendiente is null)
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.SolicitudNoEncontrada,
                "No hay una solicitud pendiente para este usuario.");
        }

        try
        {
            var ahora = DateTimeOffset.UtcNow;
            usuario.Rechazar(administradorId, solicitud.Motivo);
            pendiente.Rechazar(administradorId, solicitud.Motivo, ahora);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(UsuarioAdminErrorCodes.Validacion, ex.Message);
        }
    }

    public async Task<Result> CambiarRolAsync(
        Guid usuarioId,
        Guid administradorId,
        CambiarRolDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!TryParseRol(solicitud.Rol, out var rol))
        {
            return Result.Failure(UsuarioAdminErrorCodes.Validacion, "El rol indicado no es valido.");
        }

        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.NoEncontrado, "El usuario no existe.");
        }

        if (usuario.Rol == RolUsuario.Administrador &&
            rol != RolUsuario.Administrador &&
            await EsUltimoAdministradorAsync(usuarioId, cancellationToken))
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.UltimoAdministrador,
                "No se puede quitar el rol al ultimo Administrador activo.");
        }

        usuario.CambiarRol(rol);

        if (rol is RolUsuario.Donante or RolUsuario.Administrador)
        {
            await ReemplazarAsignacionesAsync(usuarioId, [], cancellationToken);
        }
        else if (rol == RolUsuario.Operador)
        {
            var asignaciones = await dbContext
                .Set<AsignacionComedor>()
                .Where(a => a.UsuarioId == usuarioId && a.Activa)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var extra in asignaciones.Skip(1))
            {
                extra.Desactivar();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarEstadoAsync(
        Guid usuarioId,
        Guid administradorId,
        CambiarEstadoUsuarioDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!TryParseEstado(solicitud.Estado, out var estado))
        {
            return Result.Failure(UsuarioAdminErrorCodes.Validacion, "El estado indicado no es valido.");
        }

        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.NoEncontrado, "El usuario no existe.");
        }

        if (usuario.Rol == RolUsuario.Administrador &&
            estado != EstadoUsuario.Activo &&
            await EsUltimoAdministradorAsync(usuarioId, cancellationToken))
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.UltimoAdministrador,
                "No se puede desactivar al ultimo Administrador activo.");
        }

        if (estado == EstadoUsuario.Activo)
        {
            usuario.Activar();
        }
        else if (estado == EstadoUsuario.Bloqueado)
        {
            usuario.Bloquear();
        }
        else
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.Validacion,
                "Solo se puede activar o bloquear una cuenta desde esta accion.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ActualizarAsignacionesAsync(
        Guid usuarioId,
        ActualizarAsignacionesDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.NoEncontrado, "El usuario no existe.");
        }

        if (usuario.Rol is not (RolUsuario.Operador or RolUsuario.Coordinador))
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.AsignacionInvalida,
                "Solo Operadores y Coordinadores reciben comedores.");
        }

        var comedorIds = solicitud.ComedorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (usuario.Rol == RolUsuario.Operador && comedorIds.Count > 1)
        {
            return Result.Failure(
                UsuarioAdminErrorCodes.AsignacionInvalida,
                "Un Operador solo puede tener un comedor asignado.");
        }

        var validacion = await ValidarComedoresAsync(comedorIds, cancellationToken);
        if (validacion is not null)
        {
            return Result.Failure(UsuarioAdminErrorCodes.AsignacionInvalida, validacion);
        }

        await ReemplazarAsignacionesAsync(usuarioId, comedorIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ComedorOpcionDto>>> ListarComedoresAsync(
        CancellationToken cancellationToken = default)
    {
        var comedores = await dbContext
            .Set<Comedor>()
            .AsNoTracking()
            .Select(c => new ComedorOpcionDto { Id = c.Id, Nombre = c.Nombre })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ComedorOpcionDto>>.Success(
            comedores.OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    private async Task<IReadOnlyList<UsuarioAdminDto>> MapearUsuariosAsync(
        IReadOnlyList<Usuario> usuarios,
        CancellationToken cancellationToken)
    {
        if (usuarios.Count == 0)
        {
            return [];
        }

        var ids = usuarios.Select(u => u.Id).ToList();
        var filas = await (
            from asignacion in dbContext.Set<AsignacionComedor>().AsNoTracking()
            join comedor in dbContext.Set<Comedor>().AsNoTracking()
                on asignacion.ComedorId equals comedor.Id
            where asignacion.Activa && ids.Contains(asignacion.UsuarioId)
            select new
            {
                asignacion.UsuarioId,
                ComedorId = comedor.Id,
                comedor.Nombre
            })
            .ToListAsync(cancellationToken);

        var porUsuario = filas
            .GroupBy(f => f.UsuarioId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ComedorOpcionDto>)g
                    .Select(f => new ComedorOpcionDto { Id = f.ComedorId, Nombre = f.Nombre })
                    .OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

        return usuarios
            .Select(u => new UsuarioAdminDto
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Correo = u.Correo,
                Rol = u.Rol.ToString(),
                Estado = u.Estado.ToString(),
                Organizacion = u.Organizacion,
                FechaRegistro = u.CreatedAt,
                UltimoAcceso = u.UltimoAcceso,
                Comedores = porUsuario.TryGetValue(u.Id, out var comedores) ? comedores : []
            })
            .ToList();
    }

    private async Task ReemplazarAsignacionesAsync(
        Guid usuarioId,
        IReadOnlyList<Guid> comedorIds,
        CancellationToken cancellationToken)
    {
        var actuales = await dbContext
            .Set<AsignacionComedor>()
            .Where(a => a.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);

        foreach (var asignacion in actuales)
        {
            if (comedorIds.Contains(asignacion.ComedorId))
            {
                asignacion.Reactivar();
            }
            else
            {
                asignacion.Desactivar();
            }
        }

        var existentes = actuales.Select(a => a.ComedorId).ToHashSet();

        foreach (var comedorId in comedorIds.Where(id => !existentes.Contains(id)))
        {
            dbContext.Set<AsignacionComedor>().Add(AsignacionComedor.Crear(usuarioId, comedorId));
        }
    }

    private async Task<string?> ValidarComedoresAsync(
        IReadOnlyList<Guid> comedorIds,
        CancellationToken cancellationToken)
    {
        if (comedorIds.Count == 0)
        {
            return null;
        }

        var existentes = await dbContext
            .Set<Comedor>()
            .CountAsync(c => comedorIds.Contains(c.Id), cancellationToken);

        return existentes == comedorIds.Count
            ? null
            : "Uno de los comedores seleccionados no existe.";
    }

    private async Task<bool> EsUltimoAdministradorAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var otros = await dbContext
            .Set<Usuario>()
            .CountAsync(
                u => u.Id != usuarioId &&
                     u.Rol == RolUsuario.Administrador &&
                     u.Estado == EstadoUsuario.Activo,
                cancellationToken);

        return otros == 0;
    }

    private static bool TryParseRol(string valor, out RolUsuario rol)
    {
        rol = default;
        return Enum.TryParse(valor?.Trim(), ignoreCase: true, out rol) &&
               rol is RolUsuario.Donante or RolUsuario.Operador or RolUsuario.Coordinador or RolUsuario.Administrador;
    }

    private static bool TryParseEstado(string valor, out EstadoUsuario estado)
    {
        estado = default;
        return Enum.TryParse(valor?.Trim(), ignoreCase: true, out estado) &&
               estado is EstadoUsuario.Activo or EstadoUsuario.Bloqueado;
    }
}
