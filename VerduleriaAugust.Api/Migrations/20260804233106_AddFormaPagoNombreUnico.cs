using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFormaPagoNombreUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FormasPago_Nombre",
                table: "FormasPago",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormasPago_Nombre",
                table: "FormasPago");
        }
    }
}
