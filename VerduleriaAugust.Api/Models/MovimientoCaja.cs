namespace VerduleriaAugust.Api.Models;

public sealed class MovimientoCaja
{
    public int Id { get; set; }
    public int SesionCajaId { get; set; }
    public int UsuarioId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public bool Anulado { get; set; }
    public int? UsuarioAnulacionId { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public SesionCaja? SesionCaja { get; set; }
    public Usuario? Usuario { get; set; }
    public Usuario? UsuarioAnulacion { get; set; }
}
