namespace VerduleriaAugust.Api.Models;

public class Caja
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Codigo { get; set; } = string.Empty;

    public string? PcIdentificador { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}