namespace VerduleriaAugust.Api.Models;

public sealed class RecepcionMercaderiaCorreccion
{
    public int Id { get; set; }
    public int RecepcionMercaderiaId { get; set; }
    public int UsuarioId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public RecepcionMercaderia? RecepcionMercaderia { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<RecepcionMercaderiaCorreccionDetalle> Detalles { get; set; } = new List<RecepcionMercaderiaCorreccionDetalle>();
}

public sealed class RecepcionMercaderiaCorreccionDetalle
{
    public int Id { get; set; }
    public int RecepcionMercaderiaCorreccionId { get; set; }
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public RecepcionMercaderiaCorreccion? RecepcionMercaderiaCorreccion { get; set; }
    public Producto? Producto { get; set; }
}
