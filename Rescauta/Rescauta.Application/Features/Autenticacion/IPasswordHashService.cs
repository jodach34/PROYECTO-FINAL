namespace Rescauta.Application.Features.Autenticacion;

public interface IPasswordHashService
{
    string Hash(string password);

    bool Verify(string hash, string password);
}
