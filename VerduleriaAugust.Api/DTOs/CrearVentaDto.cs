namespace VerduleriaAugust.Api.DTOs;

public class CrearVentaDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^[0-9a-fA-F-]{36}$", ErrorMessage = "IdempotencyKey debe ser un GUID válido.")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int CajaId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal Descuento { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(300)]
    public string? MotivoDescuento { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(1)]
    public List<CrearVentaItemDto> Items { get; set; } = new();

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(1)]
    public List<CrearPagoVentaDto> Pagos { get; set; } = new();
}
