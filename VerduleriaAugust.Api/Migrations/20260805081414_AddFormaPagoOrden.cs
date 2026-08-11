using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFormaPagoOrden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "FormasPago",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Orden",
                table: "FormasPago");
        }
    }
}
