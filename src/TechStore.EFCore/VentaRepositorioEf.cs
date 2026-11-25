using Microsoft.EntityFrameworkCore;
using TechStore.Core;
using TechStore.Core.Entidades;
using TechStore.Core.Excepciones;
using TechStore.Core.Interfaces;

namespace TechStore.EFCore;

/// <summary>
/// Venta transaccional con Database.BeginTransaction(). Mismo comportamiento
/// que VentaRepositorioAdo: se inserta la venta, se descuenta el stock de cada
/// producto solo si alcanza, se agregan los detalles y se calcula el total.
/// Si algo falla, Rollback: no queda guardado nada.
/// </summary>
public class VentaRepositorioEf : IVentaRepositorio
{
    private readonly Func<TechStoreContext> _crearContexto;

    public VentaRepositorioEf(Func<TechStoreContext> crearContexto)
    {
        _crearContexto = crearContexto;
    }

    public int RegistrarVenta(int idCliente, List<(int IdProducto, int Cantidad)> items)
    {
        var lineas = Utilidades.ConsolidarItems(items);

        using var ctx = _crearContexto();
        using var tx = ctx.Database.BeginTransaction();

        try
        {
            // 1. Cabecera. Fecha queda en default(DateTime): EF la omite del INSERT
            //    y SQL Server aplica DEFAULT GETDATE().
            var venta = new Venta { IdCliente = idCliente, Total = 0m };
            ctx.Ventas.Add(venta);
            ctx.SaveChanges();

            // 2a. Precio y stock de todos los productos en UNA consulta (IN / OPENJSON).
            var ids = lineas.Select(l => l.IdProducto).ToList();
            var productos = ctx.Productos
                .AsNoTracking()
                .Where(p => ids.Contains(p.IdProducto))
                .Select(p => new { p.IdProducto, p.Precio, p.Stock })
                .ToDictionary(p => p.IdProducto);

            decimal total = 0m;

            foreach (var (idProducto, cantidad) in lineas)
            {
                if (!productos.TryGetValue(idProducto, out var producto))
                    throw new InvalidOperationException($"El producto {idProducto} no existe.");

                // 2b. Descuento atómico: UPDATE ... SET Stock = Stock - @c WHERE ... AND Stock >= @c
                //     ExecuteUpdate participa en la transacción abierta del contexto.
                int filas = ctx.Productos
                    .Where(p => p.IdProducto == idProducto && p.Stock >= cantidad)
                    .ExecuteUpdate(s => s.SetProperty(p => p.Stock, p => p.Stock - cantidad));

                if (filas == 0)
                    throw new StockInsuficienteException(idProducto, producto.Stock, cantidad);

                // 2c. Detalle (se insertará en el próximo SaveChanges)
                venta.Detalles.Add(new DetalleVenta
                {
                    IdProducto = idProducto,
                    Cantidad = cantidad,
                    PrecioUnitario = producto.Precio
                });

                total += producto.Precio * cantidad;
            }

            // 3. Total + detalles en un solo SaveChanges (EF los agrupa en un lote)
            venta.Total = total;
            ctx.SaveChanges();

            // 4. Confirmar
            tx.Commit();
            return venta.IdVenta;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public int ContarVentas()
    {
        using var ctx = _crearContexto();
        return ctx.Ventas.Count();
    }
}
