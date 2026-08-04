namespace VerduleriaAugust.Api.Models;

public class FormaPago
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public ICollection<PagoVenta> PagosVenta { get; set; } = new List<PagoVenta>();
}