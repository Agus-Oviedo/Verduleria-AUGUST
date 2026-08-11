namespace VerduleriaAugust.Api.DTOs;

public class CambiarPasswordDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PasswordActual { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(10)]
    public string PasswordNueva { get; set; } = string.Empty;
}
