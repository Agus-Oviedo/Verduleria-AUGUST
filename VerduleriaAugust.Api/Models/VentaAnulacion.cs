namespace VerduleriaAugust.Api.Models;

public class VentaAnulacion
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int UsuarioId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime FechaAnulacion { get; set; }
    public Venta? Venta { get; set; }
    public Usuario? Usuario { get; set; }
}
