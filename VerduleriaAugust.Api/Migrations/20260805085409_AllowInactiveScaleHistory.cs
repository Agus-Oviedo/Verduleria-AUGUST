using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowInactiveScaleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Balanzas_Caja",
                table: "Balanzas");

            migrationBuilder.CreateIndex(
                name: "UQ_Balanzas_Caja",
                table: "Balanzas",
                column: "CajaId",
                unique: true,
                filter: "[Activa] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Balanzas_Caja",
                table: "Balanzas");

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Balanzas_Caja",
                table: "Balanzas",
                column: "CajaId");
        }
    }
}
