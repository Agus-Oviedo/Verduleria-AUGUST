namespace VerduleriaAugust.Api.DTOs;

public class AjusteStockDto
{
    public int ProductoId { get; set; }

    public decimal NuevoStock { get; set; }

    public string? Motivo { get; set; }
}