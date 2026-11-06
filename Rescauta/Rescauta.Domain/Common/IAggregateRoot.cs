namespace Rescauta.Domain.Common;

/// <summary>
/// Marca las entidades que son raiz de agregado. Solo las raices pueden obtener DbSet propio
/// en la capa Application; las entidades hijas se acceden a traves de la raiz.
/// </summary>
public interface IAggregateRoot;
