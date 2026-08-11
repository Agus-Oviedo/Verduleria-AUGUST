namespace VerduleriaAugust.Api.Models;

public class FormaPago
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public bool EsEfectivo { get; set; }

    public int Orden { get; set; }

    public ICollection<PagoVenta> PagosVenta { get; set; } = new List<PagoVenta>();
}
