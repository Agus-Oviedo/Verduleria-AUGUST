using Microsoft.EntityFrameworkCore;

namespace VerduleriaAugust.Api.Models;

public class AugustDbContext : DbContext
{
    public AugustDbContext(DbContextOptions<AugustDbContext> options)
        : base(options)
    {
    }

    public DbSet<Categoria> Categorias { get; set; }

    public DbSet<Producto> Productos { get; set; }

    public DbSet<Stock> Stock { get; set; }

    public DbSet<MovimientoStock> MovimientosStock { get; set; }

    public DbSet<Caja> Cajas { get; set; }

    public DbSet<Venta> Ventas { get; set; }

    public DbSet<VentaDetalle> VentaDetalles { get; set; }

    public DbSet<FormaPago> FormasPago { get; set; }

    public DbSet<PagoVenta> PagosVenta { get; set; }

    public DbSet<Usuario> Usuarios { get; set; }

    public DbSet<VentaAnulacion> VentaAnulaciones { get; set; }

    public DbSet<SesionCaja> SesionesCaja { get; set; }

    public DbSet<HistorialPrecio> HistorialPrecios { get; set; }

    public DbSet<Balanza> Balanzas { get; set; }

    public DbSet<AuditoriaUsuario> AuditoriasUsuario { get; set; }
    public DbSet<MovimientoCaja> MovimientosCaja { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<RecepcionMercaderia> RecepcionesMercaderia { get; set; }
    public DbSet<RecepcionMercaderiaDetalle> RecepcionesMercaderiaDetalles { get; set; }
    public DbSet<RecepcionMercaderiaEvento> RecepcionesMercaderiaEventos { get; set; }
    public DbSet<RecepcionMercaderiaCorreccion> RecepcionesMercaderiaCorrecciones { get; set; }
    public DbSet<RecepcionMercaderiaCorreccionDetalle> RecepcionesMercaderiaCorreccionesDetalles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurarCategoria(modelBuilder);
        ConfigurarProducto(modelBuilder);
        ConfigurarStock(modelBuilder);
        ConfigurarMovimientoStock(modelBuilder);
        ConfigurarCaja(modelBuilder);
        ConfigurarVenta(modelBuilder);
        ConfigurarVentaDetalle(modelBuilder);
        ConfigurarFormaPago(modelBuilder);
        ConfigurarPagoVenta(modelBuilder);
        ConfigurarUsuario(modelBuilder);
        ConfigurarVentaAnulacion(modelBuilder);
        ConfigurarSesionCaja(modelBuilder);
        ConfigurarHistorialPrecio(modelBuilder);
        ConfigurarBalanza(modelBuilder);
        ConfigurarAuditoriaUsuario(modelBuilder);
        ConfigurarMovimientoCaja(modelBuilder);
        ConfigurarRecepcionesMercaderia(modelBuilder);
    }

    private static void ConfigurarCategoria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("Categorias");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Activa)
                .HasDefaultValue(true);

            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");
        });
    }

    private static void ConfigurarProducto(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(e => e.Codigo)
                .IsUnique();

            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.TipoVenta)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.PrecioPorKilo)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.PrecioPorUnidad)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Activo)
                .HasDefaultValue(true);

            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.HasOne(e => e.Categoria)
                .WithMany(e => e.Productos)
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarStock(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stock");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.StockActual)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(e => e.UnidadMedida)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.StockMinimo)
                .HasColumnType("decimal(18,3)");

            entity.Property(e => e.FechaActualizacion)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasIndex(e => e.ProductoId)
                .IsUnique();

            entity.HasOne(e => e.Producto)
                .WithOne(e => e.Stock)
                .HasForeignKey<Stock>(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarMovimientoStock(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovimientoStock>(entity =>
        {
            entity.ToTable("MovimientosStock");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.TipoMovimiento)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Cantidad)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(e => e.StockAnterior)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(e => e.StockNuevo)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(e => e.Motivo)
                .HasMaxLength(200);

            entity.Property(e => e.FechaMovimiento)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.HasOne(e => e.Producto)
                .WithMany()
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Caja>(entity =>
        {
            entity.ToTable("Cajas");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(e => e.Codigo)
                .IsUnique();

            entity.Property(e => e.PcIdentificador)
                .HasMaxLength(100);

            entity.Property(e => e.Activa)
                .HasDefaultValue(true);

            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("SYSDATETIME()");
        });
    }

    private static void ConfigurarVenta(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Venta>(entity =>
        {
            entity.ToTable("Ventas");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.NumeroVenta)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(e => e.NumeroVenta)
                .IsUnique();

            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(36);

            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            entity.Property(e => e.FechaVenta)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.Property(e => e.Subtotal)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Descuento)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.MotivoDescuento)
                .HasMaxLength(300);

            entity.Property(e => e.Total)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("Finalizada")
                .IsRequired();

            entity.HasOne(e => e.Caja)
                .WithMany(e => e.Ventas)
                .HasForeignKey(e => e.CajaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SesionCaja)
                .WithMany(e => e.Ventas)
                .HasForeignKey(e => e.SesionCajaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UsuarioDescuento)
                .WithMany()
                .HasForeignKey(e => e.UsuarioDescuentoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarVentaDetalle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VentaDetalle>(entity =>
        {
            entity.ToTable("VentaDetalles");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.TipoVenta)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Cantidad)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(e => e.PrecioUnitario)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.TotalLinea)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.HasOne(e => e.Venta)
                .WithMany(e => e.Detalles)
                .HasForeignKey(e => e.VentaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Producto)
                .WithMany()
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarFormaPago(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormaPago>(entity =>
        {
            entity.ToTable("FormasPago");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.Nombre)
                .IsUnique();

            entity.Property(e => e.Activa)
                .HasDefaultValue(true);

            entity.Property(e => e.EsEfectivo)
                .HasDefaultValue(false);

            entity.Property(e => e.Orden)
                .HasDefaultValue(0);
        });
    }

    private static void ConfigurarPagoVenta(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PagoVenta>(entity =>
        {
            entity.ToTable("PagosVenta");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Importe)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.HasOne(e => e.Venta)
                .WithMany(e => e.Pagos)
                .HasForeignKey(e => e.VentaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.FormaPago)
                .WithMany(e => e.PagosVenta)
                .HasForeignKey(e => e.FormaPagoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarRecepcionesMercaderia(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Proveedor>(e => { e.ToTable("Proveedores"); e.Property(x=>x.Nombre).HasMaxLength(150).IsRequired(); e.Property(x=>x.Cuit).HasMaxLength(20); e.Property(x=>x.Contacto).HasMaxLength(150); e.HasIndex(x=>x.Nombre); });
        modelBuilder.Entity<RecepcionMercaderia>(e => { e.ToTable("RecepcionesMercaderia"); e.Property(x=>x.NumeroRecepcion).HasMaxLength(50).IsRequired(); e.HasIndex(x=>x.NumeroRecepcion).IsUnique(); e.Property(x=>x.Comprobante).HasMaxLength(100); e.Property(x=>x.Observaciones).HasMaxLength(500); e.Property(x=>x.TotalCosto).HasColumnType("decimal(18,2)"); e.Property(x=>x.Estado).HasMaxLength(20).IsRequired(); e.HasOne(x=>x.Proveedor).WithMany().HasForeignKey(x=>x.ProveedorId).OnDelete(DeleteBehavior.Restrict); e.HasOne(x=>x.Usuario).WithMany().HasForeignKey(x=>x.UsuarioId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<RecepcionMercaderiaDetalle>(e => { e.ToTable("RecepcionesMercaderiaDetalles"); e.Property(x=>x.Cantidad).HasColumnType("decimal(18,3)"); e.Property(x=>x.CostoUnitario).HasColumnType("decimal(18,2)"); e.Property(x=>x.TotalCosto).HasColumnType("decimal(18,2)"); e.HasIndex(x=>new{x.RecepcionMercaderiaId,x.ProductoId}).IsUnique(); e.HasOne(x=>x.RecepcionMercaderia).WithMany(x=>x.Detalles).HasForeignKey(x=>x.RecepcionMercaderiaId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x=>x.Producto).WithMany().HasForeignKey(x=>x.ProductoId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<RecepcionMercaderiaEvento>(e => { e.ToTable("RecepcionesMercaderiaEventos"); e.Property(x=>x.Tipo).HasMaxLength(30).IsRequired(); e.Property(x=>x.Detalle).HasMaxLength(1000).IsRequired(); e.Property(x=>x.Fecha).HasDefaultValueSql("SYSDATETIME()"); e.HasIndex(x=>new{x.RecepcionMercaderiaId,x.Fecha}); e.HasOne(x=>x.RecepcionMercaderia).WithMany(x=>x.Eventos).HasForeignKey(x=>x.RecepcionMercaderiaId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x=>x.Usuario).WithMany().HasForeignKey(x=>x.UsuarioId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<RecepcionMercaderiaCorreccion>(e => { e.ToTable("RecepcionesMercaderiaCorrecciones"); e.Property(x=>x.Motivo).HasMaxLength(300).IsRequired(); e.Property(x=>x.Fecha).HasDefaultValueSql("SYSDATETIME()"); e.HasIndex(x=>new{x.RecepcionMercaderiaId,x.Fecha}); e.HasOne(x=>x.RecepcionMercaderia).WithMany(x=>x.Correcciones).HasForeignKey(x=>x.RecepcionMercaderiaId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x=>x.Usuario).WithMany().HasForeignKey(x=>x.UsuarioId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<RecepcionMercaderiaCorreccionDetalle>(e => { e.ToTable("RecepcionesMercaderiaCorreccionesDetalles"); e.Property(x=>x.Cantidad).HasColumnType("decimal(18,3)"); e.HasIndex(x=>new{x.RecepcionMercaderiaCorreccionId,x.ProductoId}).IsUnique(); e.HasOne(x=>x.RecepcionMercaderiaCorreccion).WithMany(x=>x.Detalles).HasForeignKey(x=>x.RecepcionMercaderiaCorreccionId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x=>x.Producto).WithMany().HasForeignKey(x=>x.ProductoId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigurarMovimientoCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MovimientoCaja>(entity =>
        {
            entity.ToTable("MovimientosCaja");
            entity.Property(e => e.Tipo).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Importe).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Motivo).HasMaxLength(300).IsRequired();
            entity.Property(e => e.MotivoAnulacion).HasMaxLength(300);
            entity.Property(e => e.Fecha).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => new { e.SesionCajaId, e.Fecha });
            entity.HasOne(e => e.SesionCaja).WithMany().HasForeignKey(e => e.SesionCajaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UsuarioAnulacion).WithMany().HasForeignKey(e => e.UsuarioAnulacionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarUsuario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NombreUsuario).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NombreUsuarioNormalizado).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.NombreUsuarioNormalizado).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.DebeCambiarPassword).HasDefaultValue(false);
            entity.Property(e => e.Rol).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");
        });
    }

    private static void ConfigurarVentaAnulacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VentaAnulacion>(entity =>
        {
            entity.ToTable("VentaAnulaciones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Motivo).HasMaxLength(300).IsRequired();
            entity.Property(e => e.FechaAnulacion).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.VentaId).IsUnique();
            entity.HasOne(e => e.Venta)
                .WithOne(e => e.Anulacion)
                .HasForeignKey<VentaAnulacion>(e => e.VentaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarSesionCaja(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SesionCaja>(entity =>
        {
            entity.ToTable("SesionesCaja");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SaldoInicial).HasColumnType("decimal(18,2)");
            entity.Property(e => e.EfectivoEsperado).HasColumnType("decimal(18,2)");
            entity.Property(e => e.EfectivoDeclarado).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Diferencia).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Estado).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ObservacionesCierre).HasMaxLength(500);
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(e => e.CajaId)
                .IsUnique()
                .HasFilter("[Estado] = 'Abierta'");
            entity.HasOne(e => e.Caja)
                .WithMany(e => e.Sesiones)
                .HasForeignKey(e => e.CajaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UsuarioApertura)
                .WithMany()
                .HasForeignKey(e => e.UsuarioAperturaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UsuarioCierre)
                .WithMany()
                .HasForeignKey(e => e.UsuarioCierreId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarHistorialPrecio(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistorialPrecio>(entity =>
        {
            entity.ToTable("HistorialPrecios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrecioPorKiloAnterior).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PrecioPorKiloNuevo).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PrecioPorUnidadAnterior).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PrecioPorUnidadNuevo).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Motivo).HasMaxLength(300).IsRequired();
            entity.Property(e => e.FechaCambio).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => new { e.ProductoId, e.FechaCambio });
            entity.HasOne(e => e.Producto)
                .WithMany(e => e.HistorialPrecios)
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarBalanza(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Balanza>(entity =>
        {
            entity.ToTable("Balanzas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Marca).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Modelo).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PuertoCom).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Paridad).HasMaxLength(20).IsRequired();
            entity.Property(e => e.StopBits).HasMaxLength(20).IsRequired();
            entity.Property(e => e.NumeroSerie).HasMaxLength(100);
            entity.Property(e => e.ApiKeyHash).HasMaxLength(64);
            entity.HasIndex(e => e.ApiKeyHash)
                .IsUnique()
                .HasFilter("[ApiKeyHash] IS NOT NULL");
            entity.Property(e => e.Activa).HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.NumeroSerie)
                .IsUnique()
                .HasFilter("[NumeroSerie] IS NOT NULL");
            entity.HasIndex(e => e.CajaId)
                .IsUnique()
                .HasFilter("[Activa] = 1")
                .HasDatabaseName("UQ_Balanzas_Caja");
            entity.HasOne(e => e.Caja)
                .WithMany(e => e.Balanzas)
                .HasForeignKey(e => e.CajaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarAuditoriaUsuario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditoriaUsuario>(entity =>
        {
            entity.ToTable("AuditoriasUsuario");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Accion).HasMaxLength(50).IsRequired();
            entity.Property(audit => audit.Detalle).HasMaxLength(500).IsRequired();
            entity.Property(audit => audit.Fecha).HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(audit => new { audit.UsuarioObjetivoId, audit.Fecha });
            entity.HasIndex(audit => audit.UsuarioActorId);
            entity.HasOne(audit => audit.UsuarioObjetivo)
                .WithMany()
                .HasForeignKey(audit => audit.UsuarioObjetivoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(audit => audit.UsuarioActor)
                .WithMany()
                .HasForeignKey(audit => audit.UsuarioActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
