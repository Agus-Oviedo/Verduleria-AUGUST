using System.ComponentModel.DataAnnotations;
namespace VerduleriaAugust.Api.DTOs;
public sealed class ActualizarUsuarioDto
{
    [Required, RegularExpression("^(Administrador|Encargado|Cajero)$")]
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
