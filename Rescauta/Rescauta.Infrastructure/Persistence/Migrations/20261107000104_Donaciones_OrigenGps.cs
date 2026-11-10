using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rescauta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Donaciones_OrigenGps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LatitudOrigen",
                table: "donaciones",
                type: "TEXT",
                precision: 9,
                scale: 6,
                nullable: true,
                comment: "Latitud GPS del punto de recogida (origen), WGS84.");

            migrationBuilder.AddColumn<decimal>(
                name: "LongitudOrigen",
                table: "donaciones",
                type: "TEXT",
                precision: 9,
                scale: 6,
                nullable: true,
                comment: "Longitud GPS del punto de recogida (origen), WGS84.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatitudOrigen",
                table: "donaciones");

            migrationBuilder.DropColumn(
                name: "LongitudOrigen",
                table: "donaciones");
        }
    }
}
