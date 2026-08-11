namespace VerduleriaAugust.Api.Models;
public sealed class RecepcionMercaderiaDetalle
{
    public int Id { get; set; }
    public int RecepcionMercaderiaId { get; set; }
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal? CostoUnitario { get; set; }
    public decimal? TotalCosto { get; set; }
    public RecepcionMercaderia? RecepcionMercaderia { get; set; }
    public Producto? Producto { get; set; }
}
