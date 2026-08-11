namespace VerduleriaAugust.Api.Models;

public class Venta
{
    public int Id { get; set; }

    public string NumeroVenta { get; set; } = string.Empty;

    public string? IdempotencyKey { get; set; }

    public int CajaId { get; set; }

    public int? SesionCajaId { get; set; }

    public DateTime FechaVenta { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public string? MotivoDescuento { get; set; }

    public int? UsuarioDescuentoId { get; set; }

    public decimal Total { get; set; }

    public string Estado { get; set; } = "Finalizada"; // Abierta, Finalizada, Anulada

    public Caja? Caja { get; set; }

    public SesionCaja? SesionCaja { get; set; }

    public Usuario? UsuarioDescuento { get; set; }

    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();

    public ICollection<PagoVenta> Pagos { get; set; } = new List<PagoVenta>();

    public VentaAnulacion? Anulacion { get; set; }
}
