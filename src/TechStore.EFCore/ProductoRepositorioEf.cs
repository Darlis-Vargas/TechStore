using Microsoft.EntityFrameworkCore;
using TechStore.Core;
using TechStore.Core.Entidades;
using TechStore.Core.Interfaces;

namespace TechStore.EFCore;

/// <summary>
/// Implementación con EF Core y LINQ.
/// Se crea un DbContext nuevo por operación (unidad de trabajo corta), igual
/// que ADO.NET abre una conexión por operación. Así la comparación es justa y
/// el ChangeTracker no acumula entidades entre una prueba y otra.
/// </summary>
public class ProductoRepositorioEf : IProductoRepositorio
{
    private readonly Func<TechStoreContext> _crearContexto;

    /// <summary>
    /// true  = consultas de solo lectura con AsNoTracking (más rápidas).
    /// false = EF guarda una copia de cada entidad en el ChangeTracker.
    /// La prueba de rendimiento 1 mide ambas.
    /// </summary>
    public bool UsarAsNoTracking { get; set; } = true;

    public string? UltimoMensaje { get; private set; }

    public ProductoRepositorioEf(Func<TechStoreContext> crearContexto)
    {
        _crearContexto = crearContexto;
    }

    private IQueryable<Producto> Productos(TechStoreContext ctx) =>
        UsarAsNoTracking ? ctx.Productos.AsNoTracking() : ctx.Productos;

    // ------------------------------------------------------------------ Lectura

    public List<Producto> ObtenerTodos()
    {
        using var ctx = _crearContexto();
        return Productos(ctx).OrderBy(p => p.IdProducto).ToList();
    }

    public List<Producto> ObtenerPrimeros(int cantidad)
    {
        using var ctx = _crearContexto();
        return Productos(ctx).OrderBy(p => p.IdProducto).Take(cantidad).ToList();
    }

    public Producto? ObtenerPorId(int id)
    {
        using var ctx = _crearContexto();
        return Productos(ctx).FirstOrDefault(p => p.IdProducto == id);
    }

    /// <summary>
    /// El IQueryable se compone dinámicamente: cada Where se agrega solo si
    /// hace falta. EF traduce todo a un único SELECT parametrizado.
    /// </summary>
    public List<Producto> Buscar(string texto, int? idCategoria)
    {
        using var ctx = _crearContexto();
        IQueryable<Producto> consulta = Productos(ctx);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = Utilidades.PatronContiene(texto);
            consulta = consulta.Where(p => EF.Functions.Like(p.Nombre, patron));
        }

        if (idCategoria.HasValue)
            consulta = consulta.Where(p => p.IdCategoria == idCategoria.Value);

        return consulta.OrderBy(p => p.IdProducto).ToList();
    }

    // ------------------------------------------------------------------ Escritura

    public int Agregar(Producto producto)
    {
        using var ctx = _crearContexto();
        ctx.Productos.Add(producto);
        ctx.SaveChanges();              // EF lee el IDENTITY generado y lo asigna
        return producto.IdProducto;
    }

    /// <summary>AddRange + un solo SaveChanges: EF agrupa los INSERT en lotes (batching).</summary>
    public int AgregarEnBloque(List<Producto> productos)
    {
        using var ctx = _crearContexto();
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;
        ctx.Productos.AddRange(productos);
        return ctx.SaveChanges();
    }

    public bool Actualizar(Producto producto)
    {
        using var ctx = _crearContexto();
        var existente = ctx.Productos.Find(producto.IdProducto);
        if (existente is null)
        {
            UltimoMensaje = $"No existe un producto con Id {producto.IdProducto}.";
            return false;
        }

        // Copia los valores; EF detecta qué columnas cambiaron y solo actualiza esas.
        ctx.Entry(existente).CurrentValues.SetValues(producto);
        ctx.SaveChanges();
        return true;
    }

    public bool Eliminar(int id)
    {
        UltimoMensaje = null;
        using var ctx = _crearContexto();

        if (ctx.DetallesVenta.Any(d => d.IdProducto == id))
        {
            UltimoMensaje = $"No se puede eliminar el producto {id}: tiene ventas registradas.";
            return false;
        }

        var producto = ctx.Productos.Find(id);
        if (producto is null)
        {
            UltimoMensaje = $"No existe un producto con Id {id}.";
            return false;
        }

        try
        {
            ctx.Productos.Remove(producto);
            ctx.SaveChanges();
            return true;
        }
        catch (DbUpdateException)   // FK violada por una venta registrada entre medio
        {
            UltimoMensaje = $"No se puede eliminar el producto {id}: tiene ventas registradas.";
            return false;
        }
    }

    /// <summary>Por defecto se usa la versión eficiente (ExecuteUpdate).</summary>
    public int AjustarPrecios(int idCategoria, decimal porcentaje) =>
        AjustarPreciosConExecuteUpdate(idCategoria, porcentaje);

    /// <summary>
    /// Versión 1 (EF "clásico"): trae todas las entidades de la categoría a memoria,
    /// las modifica y SaveChanges envía un UPDATE por fila (agrupados en lotes).
    /// </summary>
    public int AjustarPreciosConEntidades(int idCategoria, decimal porcentaje)
    {
        porcentaje = Utilidades.NormalizarPorcentaje(porcentaje);
        using var ctx = _crearContexto();

        var productos = ctx.Productos.Where(p => p.IdCategoria == idCategoria).ToList();
        foreach (var p in productos)
            p.Precio = Math.Round(p.Precio * (100 + porcentaje) / 100, 2, MidpointRounding.AwayFromZero);

        ctx.SaveChanges();
        return productos.Count;
    }

    /// <summary>
    /// Versión 2 (EF Core 7+): ExecuteUpdate genera un único UPDATE ... WHERE
    /// en la base de datos, sin cargar entidades. Equivale al SQL de ADO.NET.
    /// </summary>
    public int AjustarPreciosConExecuteUpdate(int idCategoria, decimal porcentaje)
    {
        porcentaje = Utilidades.NormalizarPorcentaje(porcentaje);
        using var ctx = _crearContexto();

        return ctx.Productos
            .Where(p => p.IdCategoria == idCategoria)
            .ExecuteUpdate(s => s.SetProperty(
                p => p.Precio,
                p => Math.Round(p.Precio * (100 + porcentaje) / 100, 2)));
    }

    // ------------------------------------------------------------------ Reporte

    public List<ReporteCategoria> ReporteVentasPorCategoria(DateTime desde, DateTime hasta)
    {
        using var ctx = _crearContexto();
        return ConsultaReporte(ctx, desde, hasta).ToList();
    }

    /// <summary>SQL que EF genera para el reporte (para incluirlo en el informe).</summary>
    public string ObtenerSqlReporte(DateTime desde, DateTime hasta)
    {
        using var ctx = _crearContexto();
        return ConsultaReporte(ctx, desde, hasta).ToQueryString();
    }

    /// <summary>SQL que EF genera para Buscar (para comparar con el de ADO.NET).</summary>
    public string ObtenerSqlBuscar(string texto, int? idCategoria)
    {
        using var ctx = _crearContexto();
        IQueryable<Producto> consulta = ctx.Productos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = Utilidades.PatronContiene(texto);
            consulta = consulta.Where(p => EF.Functions.Like(p.Nombre, patron));
        }
        if (idCategoria.HasValue)
            consulta = consulta.Where(p => p.IdCategoria == idCategoria.Value);
        return consulta.OrderBy(p => p.IdProducto).ToQueryString();
    }

    private static IQueryable<ReporteCategoria> ConsultaReporte(
        TechStoreContext ctx, DateTime desde, DateTime hasta) =>
        ctx.DetallesVenta
            .AsNoTracking()
            .Where(d => d.Venta!.Fecha >= desde && d.Venta.Fecha <= hasta)
            .GroupBy(d => d.Producto!.Categoria!.Nombre)
            .OrderByDescending(g => g.Sum(d => d.Cantidad * d.PrecioUnitario))
            .Select(g => new ReporteCategoria
            {
                Categoria = g.Key,
                UnidadesVendidas = g.Sum(d => d.Cantidad),
                TotalVendido = g.Sum(d => d.Cantidad * d.PrecioUnitario)
            });
}
