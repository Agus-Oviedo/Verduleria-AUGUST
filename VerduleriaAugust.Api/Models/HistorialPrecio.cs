namespace VerduleriaAugust.Api.Models;

public class HistorialPrecio
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public decimal? PrecioPorKiloAnterior { get; set; }
    public decimal? PrecioPorKiloNuevo { get; set; }
    public decimal? PrecioPorUnidadAnterior { get; set; }
    public decimal? PrecioPorUnidadNuevo { get; set; }
    public int UsuarioId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime FechaCambio { get; set; }
    public Producto? Producto { get; set; }
    public Usuario? Usuario { get; set; }
}
