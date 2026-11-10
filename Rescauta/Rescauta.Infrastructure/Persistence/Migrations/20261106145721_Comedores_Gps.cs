using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rescauta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Comedores_Gps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitud",
                table: "comedores",
                type: "TEXT",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m,
                comment: "Latitud GPS del comedor (WGS84).");

            migrationBuilder.AddColumn<decimal>(
                name: "Longitud",
                table: "comedores",
                type: "TEXT",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m,
                comment: "Longitud GPS del comedor (WGS84).");

            // Las bases que ya existian no traen coordenadas. Se rellenan con la ubicacion
            // real del distrito o avenida de cada comedor (OpenStreetMap), sin tocar filas
            // que ya tengan una coordenada propia. Asi el mapa no arranca con todos los
            // pines en (0, 0) despues de actualizar una instalacion con datos.
            migrationBuilder.Sql("""
                UPDATE comedores SET Latitud = -12.1553917, Longitud = -76.9724438
                WHERE Distrito = 'San Juan de Miraflores' AND Latitud = 0 AND Longitud = 0;

                UPDATE comedores SET Latitud = -12.1766437, Longitud = -76.9189677
                WHERE Distrito = 'Villa María del Triunfo' AND Latitud = 0 AND Longitud = 0;

                UPDATE comedores SET Latitud = -11.9297668, Longitud = -76.7029335
                WHERE Distrito = 'Lurigancho-Chosica' AND Latitud = 0 AND Longitud = 0;

                UPDATE comedores SET Latitud = -12.2168944, Longitud = -76.9491496
                WHERE Distrito = 'Villa El Salvador' AND Latitud = 0 AND Longitud = 0;

                UPDATE comedores SET Latitud = -12.4684687, Longitud = -76.7724919
                WHERE Distrito = 'Pucusana' AND Latitud = 0 AND Longitud = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitud",
                table: "comedores");

            migrationBuilder.DropColumn(
                name: "Longitud",
                table: "comedores");
        }
    }
}
