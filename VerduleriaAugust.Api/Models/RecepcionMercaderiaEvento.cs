namespace VerduleriaAugust.Api.Models;

public sealed class RecepcionMercaderiaEvento
{
    public int Id { get; set; }
    public int RecepcionMercaderiaId { get; set; }
    public int UsuarioId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public RecepcionMercaderia? RecepcionMercaderia { get; set; }
    public Usuario? Usuario { get; set; }
}
