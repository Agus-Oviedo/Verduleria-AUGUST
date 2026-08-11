using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerduleriaAugust.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditoriaUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditoriasUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioObjetivoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioActorId = table.Column<int>(type: "int", nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriasUsuario_Usuarios_UsuarioActorId",
                        column: x => x.UsuarioActorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriasUsuario_Usuarios_UsuarioObjetivoId",
                        column: x => x.UsuarioObjetivoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasUsuario_UsuarioActorId",
                table: "AuditoriasUsuario",
                column: "UsuarioActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasUsuario_UsuarioObjetivoId_Fecha",
                table: "AuditoriasUsuario",
                columns: new[] { "UsuarioObjetivoId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasUsuario");
        }
    }
}
