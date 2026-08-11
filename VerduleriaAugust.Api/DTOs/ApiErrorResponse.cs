using System.Text.Json.Serialization;

namespace VerduleriaAugust.Api.DTOs;

public sealed record ApiErrorResponse(
    string Mensaje,
    string Codigo,
    string TraceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errores = null,
    string? Detalle = null);
