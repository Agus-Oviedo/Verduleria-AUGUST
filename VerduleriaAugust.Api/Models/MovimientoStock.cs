namespace VerduleriaAugust.Api.Models;

public class MovimientoStock
{
    public int Id { get; set; }

    public int ProductoId { get; set; }

    public string TipoMovimiento { get; set; } = string.Empty; // Entrada, Salida, Ajuste

    public decimal Cantidad { get; set; }

    public decimal StockAnterior { get; set; }

    public decimal StockNuevo { get; set; }

    public string? Motivo { get; set; }

    public DateTime FechaMovimiento { get; set; }

    public Producto? Producto { get; set; }
}