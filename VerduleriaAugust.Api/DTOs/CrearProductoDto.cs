namespace VerduleriaAugust.Api.DTOs;

public class CrearProductoDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 1)]
    public string Codigo { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(150, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int CategoriaId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(Peso|Unidad|Ambos)$")]
    public string TipoVenta { get; set; } = string.Empty; // Peso, Unidad, Ambos

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal? PrecioPorKilo { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal? PrecioPorUnidad { get; set; }

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal StockInicial { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(Kg|Unidad)$")]
    public string UnidadMedida { get; set; } = string.Empty; // Kg, Unidad

    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
    public decimal? StockMinimo { get; set; }
}
