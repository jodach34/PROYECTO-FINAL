using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rescauta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Donaciones_Gestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asignaciones_comedor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComedorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asignaciones_comedor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "comedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Distrito = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    RacionesDiarias = table.Column<int>(type: "INTEGER", nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ContactoTelefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    MapaX = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false, comment: "Posicion horizontal del pin en el mapa, 0-100."),
                    MapaY = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false, comment: "Posicion vertical del pin en el mapa, 0-100."),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "donaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InsumoId = table.Column<Guid>(type: "TEXT", nullable: false, comment: "Insumo donado. Referencia al modulo Kardex, sin FK todavia."),
                    ComedorId = table.Column<Guid>(type: "TEXT", nullable: false, comment: "Comedor destino. Se resuelve del insumo al registrar."),
                    InsumoNombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UnidadMedida = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ComedorNombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PuntoRecojo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaDonacion = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FechaDonacionUtc = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "FechaDonacion en UTC. SQLite no ordena DateTimeOffset."),
                    Donante = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CodigoSeguimiento = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FechaConfirmacion = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FechaEnTransito = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FechaEntrega = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FechaCierre = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MotivoCierre = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_operador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Organizacion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ComedorSolicitadoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Mensaje = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RevisadoPor = table.Column<Guid>(type: "TEXT", nullable: true),
                    FechaRevision = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes_operador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Correo = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Rol = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Organizacion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UltimoAcceso = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IntentosFallidos = table.Column<int>(type: "INTEGER", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AprobadoPor = table.Column<Guid>(type: "TEXT", nullable: true),
                    FechaAprobacion = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Insumo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComedorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    UnidadMedida = table.Column<string>(type: "TEXT", nullable: false),
                    StockActual = table.Column<decimal>(type: "TEXT", nullable: false),
                    StockMaximo = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false, comment: "Capacidad de la despensa. 0 = sin tope conocido."),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insumo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Insumo_comedores_ComedorId",
                        column: x => x.ComedorId,
                        principalTable: "comedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "donaciones_historial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DonacionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EstadoNuevo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donaciones_historial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_donaciones_historial_donaciones_DonacionId",
                        column: x => x.DonacionId,
                        principalTable: "donaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_usuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiraAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevocadaAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UltimoUsoAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones_usuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sesiones_usuario_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tokens_recuperacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiraAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UsadoAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens_recuperacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tokens_recuperacion_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientoKardex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InsumoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TipoMovimiento = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<decimal>(type: "TEXT", nullable: false),
                    FechaHora = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Responsable = table.Column<string>(type: "TEXT", nullable: false),
                    Concepto = table.Column<string>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoKardex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientoKardex_Insumo_InsumoId",
                        column: x => x.InsumoId,
                        principalTable: "Insumo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asignaciones_comedor",
                table: "asignaciones_comedor",
                column: "ComedorId");

            migrationBuilder.CreateIndex(
                name: "ux_asignaciones_usuario_comedor",
                table: "asignaciones_comedor",
                columns: new[] { "UsuarioId", "ComedorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comedores_distrito",
                table: "comedores",
                column: "Distrito");

            migrationBuilder.CreateIndex(
                name: "ix_comedores_nombre",
                table: "comedores",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_codigo_seguimiento",
                table: "donaciones",
                column: "CodigoSeguimiento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_comedor",
                table: "donaciones",
                column: "ComedorId");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_estado",
                table: "donaciones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_fecha",
                table: "donaciones",
                column: "FechaDonacion");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_fecha_utc",
                table: "donaciones",
                column: "FechaDonacionUtc");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_insumo_id",
                table: "donaciones",
                column: "InsumoId");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_usuario",
                table: "donaciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ix_donaciones_historial_donacion_fecha",
                table: "donaciones_historial",
                columns: new[] { "DonacionId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_insumos_comedor_id",
                table: "Insumo",
                column: "ComedorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoKardex_InsumoId",
                table: "MovimientoKardex",
                column: "InsumoId");

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_usuario_expira",
                table: "sesiones_usuario",
                column: "ExpiraAt");

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_usuario_usuario",
                table: "sesiones_usuario",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ux_sesiones_usuario_token",
                table: "sesiones_usuario",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_operador_estado",
                table: "solicitudes_operador",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_operador_usuario",
                table: "solicitudes_operador",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_recuperacion_expira",
                table: "tokens_recuperacion",
                column: "ExpiraAt");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_recuperacion_usuario",
                table: "tokens_recuperacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ux_tokens_recuperacion_token",
                table: "tokens_recuperacion",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_estado",
                table: "usuarios",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_rol",
                table: "usuarios",
                column: "Rol");

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_correo",
                table: "usuarios",
                column: "Correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignaciones_comedor");

            migrationBuilder.DropTable(
                name: "donaciones_historial");

            migrationBuilder.DropTable(
                name: "MovimientoKardex");

            migrationBuilder.DropTable(
                name: "sesiones_usuario");

            migrationBuilder.DropTable(
                name: "solicitudes_operador");

            migrationBuilder.DropTable(
                name: "tokens_recuperacion");

            migrationBuilder.DropTable(
                name: "donaciones");

            migrationBuilder.DropTable(
                name: "Insumo");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "comedores");
        }
    }
}
