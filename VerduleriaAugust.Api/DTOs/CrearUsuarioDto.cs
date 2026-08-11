namespace VerduleriaAugust.Api.DTOs;

public class CrearUsuarioDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 3)]
    public string NombreUsuario { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(10)]
    public string Password { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(Administrador|Encargado|Cajero)$")]
    public string Rol { get; set; } = string.Empty;
}
