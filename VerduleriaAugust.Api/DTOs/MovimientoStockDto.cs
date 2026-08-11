namespace VerduleriaAugust.Api.DTOs;

public class MovimientoStockDto
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int ProductoId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.001", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 3)]
    public string? Motivo { get; set; }
}
