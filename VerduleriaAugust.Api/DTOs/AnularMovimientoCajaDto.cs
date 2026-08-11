using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public sealed class AnularMovimientoCajaDto
{
    [Required, StringLength(300, MinimumLength = 5)]
    public string Motivo { get; set; } = string.Empty;
}
