# Persistence/Configurations

Mapeo de EF Core, **una clase por entidad**, en la carpeta del módulo:

```
Configurations/
  Mapas/       PuntoRescateConfig.cs, RutaEntregaConfig.cs
  Kardex/      ArticuloConfig.cs, MovimientoInventarioConfig.cs
  Donaciones/  DonacionConfig.cs
  Compartido/  Solo lo que usan dos o mas modulos
```

Cada clase implementa `IEntityTypeConfiguration<TEntidad>` y se descubre **sola**:
`AppDbContext.OnModelCreating` llama a `ApplyConfigurationsFromAssembly`. Por eso nadie
necesita editar `AppDbContext.cs` al agregar una entidad.

## Plantilla

```csharp
public sealed class ArticuloConfig : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> builder)
    {
        builder.ToTable("articulos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Codigo).HasMaxLength(32).IsRequired();
        builder.HasIndex(a => a.Codigo).IsUnique();
    }
}
```

## Convenciones ya resueltas por el cascarón (no repetirlas)

- Esquema por defecto: `rescauta`.
- Filtro global `!IsDeleted`: ya aplicado a toda entidad que herede de `BaseEntity`.
  Para consultar borrados: `IgnoreQueryFilters()`.
- `Guid` como clave primaria (ya viene en `BaseEntity`).
- Concurrencia optimista vía `RowVersion` (ya viene en `BaseEntity`).

## Lo que si debe configurar cada módulo

- Nombre de tabla, columnas, longitudes, indices y uniques.
- Relaciones entre entidades **de su propio módulo**.
- Conversores de enum a string (recomendado: mas legible que el entero por defecto).

```csharp
builder.Property(a => a.Estado)
    .HasConversion<string()
    .HasMaxLength(32);
```
