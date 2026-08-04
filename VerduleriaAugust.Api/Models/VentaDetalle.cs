namespace VerduleriaAugust.Api.Models;

public class VentaDetalle
{
    public int Id { get; set; }

    public int VentaId { get; set; }

    public int ProductoId { get; set; }

    public string TipoVenta { get; set; } = string.Empty; // Peso, Unidad

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal TotalLinea { get; set; }

    public Venta? Venta { get; set; }

    public Producto? Producto { get; set; }
}