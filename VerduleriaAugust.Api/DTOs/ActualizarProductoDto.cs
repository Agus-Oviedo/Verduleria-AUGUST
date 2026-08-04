namespace VerduleriaAugust.Api.DTOs;

public class ActualizarProductoDto
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public int CategoriaId { get; set; }

    public string TipoVenta { get; set; } = string.Empty; // Peso, Unidad, Ambos

    public decimal? PrecioPorKilo { get; set; }

    public decimal? PrecioPorUnidad { get; set; }

    public bool Activo { get; set; }

    public decimal StockActual { get; set; }

    public string UnidadMedida { get; set; } = string.Empty; // Kg, Unidad

    public decimal? StockMinimo { get; set; }
}