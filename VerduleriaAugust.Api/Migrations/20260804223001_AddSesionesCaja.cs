using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSesionesCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SesionCajaId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsEfectivo",
                table: "FormasPago",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE [FormasPago] SET [EsEfectivo] = 1 " +
                "WHERE UPPER(LTRIM(RTRIM([Nombre]))) = 'EFECTIVO'");

            migrationBuilder.CreateTable(
                name: "SesionesCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CajaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioAperturaId = table.Column<int>(type: "int", nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SaldoInicial = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UsuarioCierreId = table.Column<int>(type: "int", nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EfectivoEsperado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EfectivoDeclarado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ObservacionesCierre = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesCaja_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionesCaja_Usuarios_UsuarioAperturaId",
                        column: x => x.UsuarioAperturaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionesCaja_Usuarios_UsuarioCierreId",
                        column: x => x.UsuarioCierreId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_SesionCajaId",
                table: "Ventas",
                column: "SesionCajaId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesCaja_CajaId",
                table: "SesionesCaja",
                column: "CajaId",
                unique: true,
                filter: "[Estado] = 'Abierta'");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesCaja_UsuarioAperturaId",
                table: "SesionesCaja",
                column: "UsuarioAperturaId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesCaja_UsuarioCierreId",
                table: "SesionesCaja",
                column: "UsuarioCierreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_SesionesCaja_SesionCajaId",
                table: "Ventas",
                column: "SesionCajaId",
                principalTable: "SesionesCaja",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_SesionesCaja_SesionCajaId",
                table: "Ventas");

            migrationBuilder.DropTable(
                name: "SesionesCaja");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_SesionCajaId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SesionCajaId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "EsEfectivo",
                table: "FormasPago");
        }
    }
}
