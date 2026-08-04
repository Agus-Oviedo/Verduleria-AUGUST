namespace VerduleriaAugust.Api.Models;

public class Producto
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public int CategoriaId { get; set; }

    public string TipoVenta { get; set; } = string.Empty; // Peso, Unidad, Ambos

    public decimal? PrecioPorKilo { get; set; }

    public decimal? PrecioPorUnidad { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public Categoria? Categoria { get; set; }

    public Stock? Stock { get; set; }
}