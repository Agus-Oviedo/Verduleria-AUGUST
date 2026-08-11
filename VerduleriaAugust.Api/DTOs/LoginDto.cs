namespace VerduleriaAugust.Api.DTOs;

public class LoginDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 3)]
    public string NombreUsuario { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    public string Password { get; set; } = string.Empty;
}
