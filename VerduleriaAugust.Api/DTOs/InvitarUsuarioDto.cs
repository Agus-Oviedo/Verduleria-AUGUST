using System.ComponentModel.DataAnnotations;
namespace VerduleriaAugust.Api.DTOs;
public sealed class InvitarUsuarioDto
{
    [Required, StringLength(100, MinimumLength = 3)] public string NombreUsuario { get; set; } = string.Empty;
    [Required, RegularExpression("^(Administrador|Encargado|Cajero)$")] public string Rol { get; set; } = string.Empty;
}
