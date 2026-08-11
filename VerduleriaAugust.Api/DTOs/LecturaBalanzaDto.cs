using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public sealed class RegistrarLecturaBalanzaDto
{
    [Required]
    public Guid LecturaId { get; set; }

    [Range(typeof(decimal), "-1000", "1000", ParseLimitsInInvariantCulture = true)]
    public decimal PesoKg { get; set; }

    public bool Estable { get; set; }

    public DateTimeOffset FechaLectura { get; set; }
}

public sealed record LecturaBalanzaRespuestaDto(
    Guid LecturaId,
    int BalanzaId,
    int CajaId,
    decimal PesoKg,
    bool Estable,
    DateTimeOffset FechaLectura,
    DateTimeOffset FechaRecepcion,
    bool Vigente);

public sealed record LecturaBalanzaAceptadaDto(
    string Mensaje,
    Guid LecturaId,
    DateTimeOffset FechaRecepcion);
