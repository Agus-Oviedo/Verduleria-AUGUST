using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecepcionesMercaderiaCorrecciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecepcionMercaderiaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionesMercaderiaCorrecciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecepcionesMercaderiaCorrecciones_RecepcionesMercaderia_RecepcionMercaderiaId",
                        column: x => x.RecepcionMercaderiaId,
                        principalTable: "RecepcionesMercaderia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecepcionesMercaderiaCorrecciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecepcionesMercaderiaCorreccionesDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecepcionMercaderiaCorreccionId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionesMercaderiaCorreccionesDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecepcionesMercaderiaCorreccionesDetalles_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecepcionesMercaderiaCorreccionesDetalles_RecepcionesMercaderiaCorrecciones_RecepcionMercaderiaCorreccionId",
                        column: x => x.RecepcionMercaderiaCorreccionId,
                        principalTable: "RecepcionesMercaderiaCorrecciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesMercaderiaCorrecciones_RecepcionMercaderiaId_Fecha",
                table: "RecepcionesMercaderiaCorrecciones",
                columns: new[] { "RecepcionMercaderiaId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesMercaderiaCorrecciones_UsuarioId",
                table: "RecepcionesMercaderiaCorrecciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesMercaderiaCorreccionesDetalles_ProductoId",
                table: "RecepcionesMercaderiaCorreccionesDetalles",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesMercaderiaCorreccionesDetalles_RecepcionMercaderiaCorreccionId_ProductoId",
                table: "RecepcionesMercaderiaCorreccionesDetalles",
                columns: new[] { "RecepcionMercaderiaCorreccionId", "ProductoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecepcionesMercaderiaCorreccionesDetalles");

            migrationBuilder.DropTable(
                name: "RecepcionesMercaderiaCorrecciones");
        }
    }
}
