namespace VerduleriaAugust.Api.DTOs;

public class MovimientoStockDto
{
    public int ProductoId { get; set; }

    public decimal Cantidad { get; set; }

    public string? Motivo { get; set; }
}