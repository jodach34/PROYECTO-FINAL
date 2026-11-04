namespace Rescauta.Application.Features.Kardex;

public sealed record UsuarioOperacion(
    Guid UsuarioId,
    string Rol,
    IReadOnlyCollection<Guid> ComedoresAsignados)
{
    public bool EsAdministrador =>
        string.Equals(Rol, "Administrador", StringComparison.OrdinalIgnoreCase);

    public bool PuedeOperar(Guid comedorId) =>
        EsAdministrador || ComedoresAsignados.Contains(comedorId);
}
