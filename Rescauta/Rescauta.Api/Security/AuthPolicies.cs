namespace Rescauta.Api.Security;

public static class AuthPolicies
{
    public const string Scheme = "RescautaBearer";
    public const string Admin = "Administrador";
    public const string Operacion = "Operacion";
    public const string Donar = "Donar";

    public const string RolDonante = "Donante";
    public const string RolOperador = "Operador";
    public const string RolCoordinador = "Coordinador";
    public const string RolAdministrador = "Administrador";
}
