using Microsoft.EntityFrameworkCore;
using TechStore.Core.Entidades;

namespace TechStore.EFCore;

/// <summary>
/// Code First: el modelo se define en C# y las migraciones crean TechStoreDB_EF.
/// Las relaciones y restricciones se configuran con Fluent API para reproducir
/// el mismo esquema que TechStoreDB.sql (mismos tipos, nombres, UNIQUE, CHECK y DEFAULT).
/// </summary>
public class TechStoreContext : DbContext
{
    public TechStoreContext(DbContextOptions<TechStoreContext> options) : base(options) { }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(e =>
        {
            e.ToTable("Categoria");
            e.HasKey(c => c.IdCategoria).HasName("PK_Categoria");
            e.Property(c => c.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(c => c.Nombre).IsUnique().HasDatabaseName("UQ_Categoria_Nombre");
        });

        modelBuilder.Entity<Producto>(e =>
        {
            e.ToTable("Producto", t =>
            {
                t.HasCheckConstraint("CK_Producto_Precio", "[Precio] > 0");
                t.HasCheckConstraint("CK_Producto_Stock", "[Stock] >= 0");
            });
            e.HasKey(p => p.IdProducto).HasName("PK_Producto");
            e.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
            e.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
            e.Property(p => p.Precio).HasPrecision(10, 2);
            e.Property(p => p.Marca).HasMaxLength(50);
            e.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UQ_Producto_Codigo");
            e.HasIndex(p => p.IdCategoria).HasDatabaseName("IX_Producto_IdCategoria");

            e.HasOne(p => p.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(p => p.IdCategoria)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_Producto_Categoria");
        });

        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("Cliente");
            e.HasKey(c => c.IdCliente).HasName("PK_Cliente");
            e.Property(c => c.Cedula).HasMaxLength(13).IsRequired();
            e.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
            e.Property(c => c.Email).HasMaxLength(100).IsRequired();
            e.HasIndex(c => c.Cedula).IsUnique().HasDatabaseName("UQ_Cliente_Cedula");
            e.HasIndex(c => c.Email).IsUnique().HasDatabaseName("UQ_Cliente_Email");
        });

        modelBuilder.Entity<Venta>(e =>
        {
            e.ToTable("Venta", t => t.HasCheckConstraint("CK_Venta_Total", "[Total] >= 0"));
            e.HasKey(v => v.IdVenta).HasName("PK_Venta");
            e.Property(v => v.Fecha).HasDefaultValueSql("GETDATE()");
            e.Property(v => v.Total).HasPrecision(12, 2);
            e.HasIndex(v => v.IdCliente).HasDatabaseName("IX_Venta_IdCliente");
            e.HasIndex(v => v.Fecha).HasDatabaseName("IX_Venta_Fecha");

            e.HasOne(v => v.Cliente)
             .WithMany(c => c.Ventas)
             .HasForeignKey(v => v.IdCliente)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_Venta_Cliente");
        });

        modelBuilder.Entity<DetalleVenta>(e =>
        {
            e.ToTable("DetalleVenta", t =>
            {
                t.HasCheckConstraint("CK_DetalleVenta_Cantidad", "[Cantidad] > 0");
                t.HasCheckConstraint("CK_DetalleVenta_PrecioUnitario", "[PrecioUnitario] > 0");
            });
            e.HasKey(d => d.IdDetalle).HasName("PK_DetalleVenta");
            e.Property(d => d.PrecioUnitario).HasPrecision(10, 2);
            e.HasIndex(d => d.IdVenta).HasDatabaseName("IX_DetalleVenta_IdVenta");
            e.HasIndex(d => d.IdProducto).HasDatabaseName("IX_DetalleVenta_IdProducto");

            e.HasOne(d => d.Venta)
             .WithMany(v => v.Detalles)
             .HasForeignKey(d => d.IdVenta)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_DetalleVenta_Venta");

            e.HasOne(d => d.Producto)
             .WithMany(p => p.Detalles)
             .HasForeignKey(d => d.IdProducto)
             .OnDelete(DeleteBehavior.Restrict)
             .HasConstraintName("FK_DetalleVenta_Producto");
        });
    }
}
