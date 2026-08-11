using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVentaDiscountAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoDescuento",
                table: "Ventas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioDescuentoId",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_UsuarioDescuentoId",
                table: "Ventas",
                column: "UsuarioDescuentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Usuarios_UsuarioDescuentoId",
                table: "Ventas",
                column: "UsuarioDescuentoId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Usuarios_UsuarioDescuentoId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_UsuarioDescuentoId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "MotivoDescuento",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "UsuarioDescuentoId",
                table: "Ventas");
        }
    }
}
