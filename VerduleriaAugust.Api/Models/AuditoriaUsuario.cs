namespace VerduleriaAugust.Api.Models;

public sealed class AuditoriaUsuario
{
    public int Id { get; set; }
    public int UsuarioObjetivoId { get; set; }
    public int? UsuarioActorId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public Usuario? UsuarioObjetivo { get; set; }
    public Usuario? UsuarioActor { get; set; }
}
