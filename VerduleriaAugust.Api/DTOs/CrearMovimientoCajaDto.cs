using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public sealed class CrearMovimientoCajaDto
{
    [Required, RegularExpression("^(Ingreso|Retiro)$")]
    public string Tipo { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Importe { get; set; }

    [Required, StringLength(300, MinimumLength = 5)]
    public string Motivo { get; set; } = string.Empty;
}
