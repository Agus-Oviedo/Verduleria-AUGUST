namespace VerduleriaAugust.Api.DTOs;

public class CrearPagoVentaDto
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int FormaPagoId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal Importe { get; set; }
}
