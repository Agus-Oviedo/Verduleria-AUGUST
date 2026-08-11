namespace VerduleriaAugust.Api.DTOs;

public class AbrirCajaDto
{
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal SaldoInicial { get; set; }
}
