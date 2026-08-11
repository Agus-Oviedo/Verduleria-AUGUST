using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanzas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Balanzas forma parte de la base original administrada desde SSMS.
            // Esta migración conserva sus columnas y agrega sólo las requeridas
            // para autenticar al agente y registrar su conexión.
            migrationBuilder.AddColumn<string>(
                name: "ApiKeyHash",
                table: "Balanzas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroSerie",
                table: "Balanzas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaConexion",
                table: "Balanzas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Balanzas_ApiKeyHash",
                table: "Balanzas",
                column: "ApiKeyHash",
                unique: true,
                filter: "[ApiKeyHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Balanzas_NumeroSerie",
                table: "Balanzas",
                column: "NumeroSerie",
                unique: true,
                filter: "[NumeroSerie] IS NOT NULL");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Balanzas_ApiKeyHash", table: "Balanzas");
            migrationBuilder.DropIndex(name: "IX_Balanzas_NumeroSerie", table: "Balanzas");
            migrationBuilder.DropColumn(name: "ApiKeyHash", table: "Balanzas");
            migrationBuilder.DropColumn(name: "NumeroSerie", table: "Balanzas");
            migrationBuilder.DropColumn(name: "UltimaConexion", table: "Balanzas");
        }
    }
}
