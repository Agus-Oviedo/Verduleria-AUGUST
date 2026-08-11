using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.Models;

public class SesionCaja
{
    public int Id { get; set; }
    public int CajaId { get; set; }
    public int UsuarioAperturaId { get; set; }
    public DateTime FechaApertura { get; set; }
    public decimal SaldoInicial { get; set; }
    public string Estado { get; set; } = "Abierta";
    public int? UsuarioCierreId { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? EfectivoEsperado { get; set; }
    public decimal? EfectivoDeclarado { get; set; }
    public decimal? Diferencia { get; set; }
    public string? ObservacionesCierre { get; set; }
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Caja? Caja { get; set; }
    public Usuario? UsuarioApertura { get; set; }
    public Usuario? UsuarioCierre { get; set; }
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
