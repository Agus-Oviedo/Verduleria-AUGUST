using Microsoft.EntityFrameworkCore;

namespace VerduleriaAugust.Api.Models;

public class AugustDbContext : DbContext
{
    public AugustDbContext(DbContextOptions<AugustDbContext> options) : base(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
                .HasForeignKey(e => e.CategoriaId);
        });

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

            entity.HasIndex(e => e.ProductoId)
                .IsUnique();

            entity.HasOne(e => e.Producto)
                .WithOne(e => e.Stock)
                .HasForeignKey<Stock>(e => e.ProductoId);
        });

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
                .HasForeignKey(e => e.ProductoId);
        });

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

        modelBuilder.Entity<Venta>(entity =>
        {
            entity.ToTable("Ventas");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.NumeroVenta)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(e => e.NumeroVenta)
                .IsUnique();

            entity.Property(e => e.FechaVenta)
                .HasDefaultValueSql("SYSDATETIME()");

            entity.Property(e => e.Subtotal)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Descuento)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Total)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("Finalizada")
                .IsRequired();

            entity.HasOne(e => e.Caja)
                .WithMany(e => e.Ventas)
                .HasForeignKey(e => e.CajaId);
        });

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
                .HasForeignKey(e => e.VentaId);

            entity.HasOne(e => e.Producto)
                .WithMany()
                .HasForeignKey(e => e.ProductoId);
        });

        modelBuilder.Entity<FormaPago>(entity =>
        {
            entity.ToTable("FormasPago");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Activa)
                .HasDefaultValue(true);
        });

        modelBuilder.Entity<PagoVenta>(entity =>
        {
            entity.ToTable("PagosVenta");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Importe)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.HasOne(e => e.Venta)
                .WithMany(e => e.Pagos)
                .HasForeignKey(e => e.VentaId);

            entity.HasOne(e => e.FormaPago)
                .WithMany(e => e.PagosVenta)
                .HasForeignKey(e => e.FormaPagoId);
        });
    }
}