namespace VerduleriaAugust.Api.Models;

public class Balanza
{
    public int Id { get; set; }
    public int CajaId { get; set; }
    public string Marca { get; set; } = "Systel";
    public string Modelo { get; set; } = string.Empty;
    public string PuertoCom { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Paridad { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string? NumeroSerie { get; set; }
    public string? ApiKeyHash { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? UltimaConexion { get; set; }
    public Caja? Caja { get; set; }
}
