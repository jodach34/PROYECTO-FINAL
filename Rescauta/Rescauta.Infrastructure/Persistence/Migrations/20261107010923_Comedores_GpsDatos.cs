using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rescauta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Comedores_GpsDatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La migracion anterior comparaba Latitud = 0 contra una columna TEXT donde EF
            // guarda "0.0", asi que en SQLite la igualdad nunca se cumplia y las bases con
            // comedores ya creados se quedaban con GPS en (0, 0). Aqui se compara el valor
            // numerico real para rellenar solo las filas que siguen sin coordenadas.
            migrationBuilder.Sql("""
                UPDATE comedores SET Latitud = -12.1553917, Longitud = -76.9724438
                WHERE Distrito = 'San Juan de Miraflores'
                  AND CAST(Latitud AS REAL) = 0 AND CAST(Longitud AS REAL) = 0;

                UPDATE comedores SET Latitud = -12.1766437, Longitud = -76.9189677
                WHERE Distrito = 'Villa María del Triunfo'
                  AND CAST(Latitud AS REAL) = 0 AND CAST(Longitud AS REAL) = 0;

                UPDATE comedores SET Latitud = -11.9297668, Longitud = -76.7029335
                WHERE Distrito = 'Lurigancho-Chosica'
                  AND CAST(Latitud AS REAL) = 0 AND CAST(Longitud AS REAL) = 0;

                UPDATE comedores SET Latitud = -12.2168944, Longitud = -76.9491496
                WHERE Distrito = 'Villa El Salvador'
                  AND CAST(Latitud AS REAL) = 0 AND CAST(Longitud AS REAL) = 0;

                UPDATE comedores SET Latitud = -12.4684687, Longitud = -76.7724919
                WHERE Distrito = 'Pucusana'
                  AND CAST(Latitud AS REAL) = 0 AND CAST(Longitud AS REAL) = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migracion de datos: no se revierte para no borrar coordenadas que el usuario
            // haya podido corregir despues.
        }
    }
}
