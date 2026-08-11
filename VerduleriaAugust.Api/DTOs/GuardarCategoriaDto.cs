using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public sealed class GuardarCategoriaDto
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;
}
