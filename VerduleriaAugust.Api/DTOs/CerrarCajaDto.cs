namespace VerduleriaAugust.Api.DTOs;

public class CerrarCajaDto
{
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal EfectivoDeclarado { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? Observaciones { get; set; }
}
