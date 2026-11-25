using System.Data;
using Microsoft.Data.SqlClient;
using TechStore.Core;
using TechStore.Core.Excepciones;
using TechStore.Core.Interfaces;

namespace TechStore.AdoNet;

/// <summary>
/// Venta transaccional con SqlTransaction.
/// Orden de los pasos (dentro de UNA transacción):
///   1. INSERT de la venta (Total = 0 provisional) y obtención del Id.
///   2. Por cada producto: leer precio y stock, descontar stock SOLO si alcanza
///      (UPDATE ... WHERE Stock >= @cantidad), insertar el detalle.
///   3. UPDATE del total de la venta.
///   4. Commit.
/// Si cualquier paso falla (stock insuficiente, producto o cliente inexistente,
/// error de red...), se hace Rollback y no queda NADA guardado.
/// </summary>
public class VentaRepositorioAdo : IVentaRepositorio
{
    private readonly string _cadenaConexion;

    public VentaRepositorioAdo(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public int RegistrarVenta(int idCliente, List<(int IdProducto, int Cantidad)> items)
    {
        var lineas = Utilidades.ConsolidarItems(items);

        using var cn = new SqlConnection(_cadenaConexion);
        cn.Open();
        using SqlTransaction tx = cn.BeginTransaction();

        try
        {
            // 1. Cabecera de la venta. Fecha la pone el DEFAULT GETDATE().
            int idVenta;
            using (var cmd = new SqlCommand(
                "INSERT INTO dbo.Venta (IdCliente, Total) VALUES (@idCliente, 0); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);", cn, tx))
            {
                cmd.Parameters.Add("@idCliente", SqlDbType.Int).Value = idCliente;
                idVenta = (int)cmd.ExecuteScalar()!;
            }

            decimal total = 0m;

            foreach (var (idProducto, cantidad) in lineas)
            {
                // 2a. Precio y stock actuales. UPDLOCK bloquea la fila hasta el
                //     Commit para que otra venta concurrente no use el mismo stock.
                decimal precio;
                int stock;
                using (var cmd = new SqlCommand(
                    "SELECT Precio, Stock FROM dbo.Producto WITH (UPDLOCK, ROWLOCK) " +
                    "WHERE IdProducto = @idProducto;", cn, tx))
                {
                    cmd.Parameters.Add("@idProducto", SqlDbType.Int).Value = idProducto;
                    using var dr = cmd.ExecuteReader();
                    if (!dr.Read())
                        throw new InvalidOperationException($"El producto {idProducto} no existe.");
                    precio = dr.GetDecimal(0);
                    stock = dr.GetInt32(1);
                }

                // 2b. Descontar stock solo si alcanza.
                using (var cmd = new SqlCommand(
                    "UPDATE dbo.Producto SET Stock = Stock - @cantidad " +
                    "WHERE IdProducto = @idProducto AND Stock >= @cantidad;", cn, tx))
                {
                    cmd.Parameters.Add("@cantidad", SqlDbType.Int).Value = cantidad;
                    cmd.Parameters.Add("@idProducto", SqlDbType.Int).Value = idProducto;
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new StockInsuficienteException(idProducto, stock, cantidad);
                }

                // 2c. Detalle con el precio vigente.
                using (var cmd = new SqlCommand(
                    "INSERT INTO dbo.DetalleVenta (IdVenta, IdProducto, Cantidad, PrecioUnitario) " +
                    "VALUES (@idVenta, @idProducto, @cantidad, @precio);", cn, tx))
                {
                    cmd.Parameters.Add("@idVenta", SqlDbType.Int).Value = idVenta;
                    cmd.Parameters.Add("@idProducto", SqlDbType.Int).Value = idProducto;
                    cmd.Parameters.Add("@cantidad", SqlDbType.Int).Value = cantidad;
                    var pPrecio = cmd.Parameters.Add("@precio", SqlDbType.Decimal);
                    pPrecio.Precision = 10;
                    pPrecio.Scale = 2;
                    pPrecio.Value = precio;
                    cmd.ExecuteNonQuery();
                }

                total += precio * cantidad;
            }

            // 3. Total calculado
            using (var cmd = new SqlCommand(
                "UPDATE dbo.Venta SET Total = @total WHERE IdVenta = @idVenta;", cn, tx))
            {
                var pTotal = cmd.Parameters.Add("@total", SqlDbType.Decimal);
                pTotal.Precision = 12;
                pTotal.Scale = 2;
                pTotal.Value = total;
                cmd.Parameters.Add("@idVenta", SqlDbType.Int).Value = idVenta;
                cmd.ExecuteNonQuery();
            }

            // 4. Todo bien: confirmar
            tx.Commit();
            return idVenta;
        }
        catch
        {
            tx.Rollback();   // deshace la venta, los detalles y los descuentos de stock
            throw;
        }
    }

    public int ContarVentas()
    {
        using var cn = new SqlConnection(_cadenaConexion);
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Venta;", cn);
        cn.Open();
        return (int)cmd.ExecuteScalar()!;
    }
}
