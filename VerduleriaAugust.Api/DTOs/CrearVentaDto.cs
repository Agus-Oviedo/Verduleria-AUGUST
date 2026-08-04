namespace VerduleriaAugust.Api.DTOs;

public class CrearVentaDto
{
    public int CajaId { get; set; }

    public decimal Descuento { get; set; }

    public List<CrearVentaItemDto> Items { get; set; } = new();

    public List<CrearPagoVentaDto> Pagos { get; set; } = new();
}