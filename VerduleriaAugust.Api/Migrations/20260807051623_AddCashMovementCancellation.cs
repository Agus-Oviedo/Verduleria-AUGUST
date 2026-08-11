using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovementCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Anulado",
                table: "MovimientosCaja",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                table: "MovimientosCaja",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "MovimientosCaja",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioAnulacionId",
                table: "MovimientosCaja",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_UsuarioAnulacionId",
                table: "MovimientosCaja",
                column: "UsuarioAnulacionId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_Usuarios_UsuarioAnulacionId",
                table: "MovimientosCaja",
                column: "UsuarioAnulacionId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_Usuarios_UsuarioAnulacionId",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_UsuarioAnulacionId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "Anulado",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "UsuarioAnulacionId",
                table: "MovimientosCaja");
        }
    }
}
