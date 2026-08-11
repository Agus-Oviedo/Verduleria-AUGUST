namespace VerduleriaAugust.Api.DTOs;

public class GuardarFormaPagoDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;
    public bool EsEfectivo { get; set; }
    public bool Activa { get; set; } = true;
    [System.ComponentModel.DataAnnotations.Range(0, 9999)]
    public int Orden { get; set; }
}
