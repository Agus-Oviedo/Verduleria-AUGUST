namespace VerduleriaAugust.Api.DTOs;

public class CrearVentaItemDto
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int ProductoId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(Peso|Unidad)$")]
    public string TipoVenta { get; set; } = string.Empty; // Peso, Unidad

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.001", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; }

    public Guid? LecturaBalanzaId { get; set; }
}
