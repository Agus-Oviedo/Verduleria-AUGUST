namespace VerduleriaAugust.Api.Models;

public class Categoria
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}