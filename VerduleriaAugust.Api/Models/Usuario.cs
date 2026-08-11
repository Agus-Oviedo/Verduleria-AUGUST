namespace VerduleriaAugust.Api.Models;

public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreUsuarioNormalizado { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public bool DebeCambiarPassword { get; set; }
    public DateTime FechaCreacion { get; set; }
}
