using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public class PaginacionDto
{
    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;

    [Range(1, 100)]
    public int TamanoPagina { get; set; } = 20;
}

public sealed class ProductosConsultaDto : PaginacionDto
{
    [StringLength(150)]
    public string? Buscar { get; set; }

    public int? CategoriaId { get; set; }

    public bool? Activo { get; set; }

    [RegularExpression("^(Peso|Unidad|Ambos)$")]
    public string? TipoVenta { get; set; }
}

public sealed class VentasConsultaDto : PaginacionDto
{
    [StringLength(50)]
    public string? Buscar { get; set; }

    public int? CajaId { get; set; }

    [RegularExpression("^(Finalizada|Anulada)$")]
    public string? Estado { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }
}

public sealed class StockConsultaDto : PaginacionDto
{
    [StringLength(150)]
    public string? Buscar { get; set; }

    public int? CategoriaId { get; set; }

    public bool? BajoStock { get; set; }
}

public sealed class MovimientosStockConsultaDto : PaginacionDto
{
    [StringLength(150)]
    public string? Buscar { get; set; }

    public int? ProductoId { get; set; }

    [StringLength(20)]
    public string? TipoMovimiento { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }
}

public sealed class CajasConsultaDto : PaginacionDto
{
    [StringLength(100)]
    public string? Buscar { get; set; }

    public bool? Activa { get; set; }

    public bool? ConSesionAbierta { get; set; }
}

public sealed class TurnosCajaConsultaDto : PaginacionDto
{
    public int? CajaId { get; set; }

    [StringLength(100)]
    public string? Usuario { get; set; }

    [RegularExpression("^(Abierta|Cerrada)$")]
    public string? Estado { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }
}

public sealed class HistorialPreciosConsultaDto : PaginacionDto
{
    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }
}

public sealed class CategoriasConsultaDto : PaginacionDto
{
    [StringLength(100)]
    public string? Buscar { get; set; }

    public bool? Activa { get; set; }
}

public sealed class FormasPagoConsultaDto : PaginacionDto
{
    [StringLength(100)]
    public string? Buscar { get; set; }

    public bool IncluirInactivas { get; set; }

    public bool? EsEfectivo { get; set; }
}

public sealed class AuditoriaUsuariosConsultaDto : PaginacionDto
{
    public int? UsuarioId { get; set; }

    [StringLength(50)]
    public string? Accion { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }
}
