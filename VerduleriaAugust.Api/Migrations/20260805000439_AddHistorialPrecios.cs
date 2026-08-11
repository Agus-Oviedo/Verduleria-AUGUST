using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorialPrecios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialPrecios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    PrecioPorKiloAnterior = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PrecioPorKiloNuevo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PrecioPorUnidadAnterior = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PrecioPorUnidadNuevo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FechaCambio = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialPrecios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialPrecios_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialPrecios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPrecios_ProductoId_FechaCambio",
                table: "HistorialPrecios",
                columns: new[] { "ProductoId", "FechaCambio" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialPrecios_UsuarioId",
                table: "HistorialPrecios",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialPrecios");
        }
    }
}
