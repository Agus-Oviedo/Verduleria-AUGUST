using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.Models;

public class Stock
{
    public int Id { get; set; }

    public int ProductoId { get; set; }

    public decimal StockActual { get; set; }

    public string UnidadMedida { get; set; } = string.Empty;

    public decimal? StockMinimo { get; set; }

    public DateTime FechaActualizacion { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Producto? Producto { get; set; }
}