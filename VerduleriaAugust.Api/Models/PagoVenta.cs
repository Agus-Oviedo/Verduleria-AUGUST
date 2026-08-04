namespace VerduleriaAugust.Api.Models;

public class PagoVenta
{
    public int Id { get; set; }

    public int VentaId { get; set; }

    public int FormaPagoId { get; set; }

    public decimal Importe { get; set; }

    public Venta? Venta { get; set; }

    public FormaPago? FormaPago { get; set; }
}