namespace VerduleriaAugust.Api.DTOs;

public sealed record RespuestaPaginada<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Pagina,
    int TamanoPagina)
{
    public int TotalPaginas => Total == 0
        ? 0
        : (int)Math.Ceiling(Total / (double)TamanoPagina);
}
