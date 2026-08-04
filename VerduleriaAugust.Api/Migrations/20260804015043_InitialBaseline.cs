using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La base de datos y sus tablas ya existen.
            // Esta migración solo establece el punto inicial
            // para comenzar a administrar cambios con EF Core.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se eliminan tablas porque esta migración
            // no creó la estructura original de la base.
        }
    }
}