namespace VerduleriaAugust.Api.Models;
public sealed class RecepcionMercaderia
{
    public int Id { get; set; }
    public string NumeroRecepcion { get; set; } = string.Empty;
    public int ProveedorId { get; set; }
    public int UsuarioId { get; set; }
    public string? Comprobante { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaRecepcion { get; set; }
    public decimal TotalCosto { get; set; }
    public string Estado { get; set; } = "Confirmada";
    public Proveedor? Proveedor { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<RecepcionMercaderiaDetalle> Detalles { get; set; } = new List<RecepcionMercaderiaDetalle>();
    public ICollection<RecepcionMercaderiaEvento> Eventos { get; set; } = new List<RecepcionMercaderiaEvento>();
    public ICollection<RecepcionMercaderiaCorreccion> Correcciones { get; set; } = new List<RecepcionMercaderiaCorreccion>();
}
