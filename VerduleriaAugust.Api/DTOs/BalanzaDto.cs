using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public sealed class GuardarBalanzaDto
{
    [Range(1, int.MaxValue)]
    public int CajaId { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string Marca { get; set; } = "Systel";

    [Required, StringLength(100, MinimumLength = 1)]
    public string Modelo { get; set; } = "Systel Croma";

    [Required, StringLength(20, MinimumLength = 1)]
    public string PuertoCom { get; set; } = "COM1";

    [Range(1, 115200)]
    public int BaudRate { get; set; } = 9600;

    [Range(5, 8)]
    public int DataBits { get; set; } = 8;

    [Required, RegularExpression("^(None|Odd|Even|Mark|Space)$")]
    public string Paridad { get; set; } = "None";

    [Required, RegularExpression("^(None|One|Two|OnePointFive)$")]
    public string StopBits { get; set; } = "One";

    [StringLength(100)]
    public string? NumeroSerie { get; set; }

    public bool Activa { get; set; } = true;
}

public sealed class BalanzasConsultaDto : PaginacionDto
{
    [StringLength(100)]
    public string? Buscar { get; set; }
    public int? CajaId { get; set; }
    public bool? Activa { get; set; }
}

public sealed record BalanzaRespuestaDto(
    int Id,
    int CajaId,
    string? Caja,
    string Marca,
    string Modelo,
    string PuertoCom,
    int BaudRate,
    int DataBits,
    string Paridad,
    string StopBits,
    string? NumeroSerie,
    bool Activa,
    DateTime FechaCreacion,
    DateTime? UltimaConexion);

public sealed record BalanzaCreadaRespuestaDto(
    string Mensaje,
    BalanzaRespuestaDto Balanza,
    string ApiKey);

public sealed record ApiKeyRegeneradaRespuestaDto(
    string Mensaje,
    int BalanzaId,
    string ApiKey);
