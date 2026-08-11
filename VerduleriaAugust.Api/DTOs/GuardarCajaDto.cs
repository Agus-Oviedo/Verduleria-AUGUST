namespace VerduleriaAugust.Api.DTOs;

public class GuardarCajaDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 1)]
    public string Codigo { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? PcIdentificador { get; set; }
    public bool Activa { get; set; } = true;
}
