namespace VerduleriaAugust.Api.DTOs;

public class AnularVentaDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(300, MinimumLength = 5)]
    public string Motivo { get; set; } = string.Empty;
}
