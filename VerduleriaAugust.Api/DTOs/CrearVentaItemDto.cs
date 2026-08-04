namespace VerduleriaAugust.Api.DTOs;

public class CrearVentaItemDto
{
    public int ProductoId { get; set; }

    public string TipoVenta { get; set; } = string.Empty; // Peso, Unidad

    public decimal Cantidad { get; set; }
}