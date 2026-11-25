using System.Data;
using Microsoft.Data.SqlClient;

namespace TechStore.Consola;

/// <summary>
/// Operaciones auxiliares de las pruebas de rendimiento (preparar y limpiar datos).
/// Se ejecutan FUERA del cronómetro y siempre con ADO.NET, sobre la base que
/// corresponda (TechStoreDB o TechStoreDB_EF). Todo el SQL es fijo o parametrizado.
/// </summary>
public static class DatosPrueba
{
    public const string PrefijoCodigoPrueba = "BX";

    public static List<int> IdsProducto(string cs) =>
        ListaEnteros(cs, "SELECT IdProducto FROM dbo.Producto WHERE Codigo NOT LIKE N'BX%' ORDER BY IdProducto;");

    public static List<int> IdsCliente(string cs) =>
        ListaEnteros(cs, "SELECT IdCliente FROM dbo.Cliente ORDER BY IdCliente;");

    public static int PrimeraCategoria(string cs) =>
        Escalar(cs, "SELECT ISNULL(MIN(IdCategoria), 0) FROM dbo.Categoria;");

    public static int ContarProductos(string cs) =>
        Escalar(cs, "SELECT COUNT(*) FROM dbo.Producto;");

    public static int MaxIdVenta(string cs) =>
        Escalar(cs, "SELECT ISNULL(MAX(IdVenta), 0) FROM dbo.Venta;");

    public static void EliminarProductosPrueba(string cs) =>
        Ejecutar(cs, "DELETE FROM dbo.Producto WHERE Codigo LIKE N'BX%';");

    // --- Escenario 5: guardar y restaurar los precios para que cada repetición parta igual
    public static void RespaldarPrecios(string cs, int idCategoria) =>
        Ejecutar(cs,
            "DROP TABLE IF EXISTS dbo.BenchRespaldoPrecios; " +
            "SELECT IdProducto, Precio INTO dbo.BenchRespaldoPrecios " +
            "FROM dbo.Producto WHERE IdCategoria = @idCategoria;",
            ("@idCategoria", idCategoria));

    public static void RestaurarPrecios(string cs) =>
        Ejecutar(cs,
            "UPDATE p SET p.Precio = r.Precio " +
            "FROM dbo.Producto AS p JOIN dbo.BenchRespaldoPrecios AS r ON r.IdProducto = p.IdProducto;");

    public static void EliminarRespaldoPrecios(string cs) =>
        Ejecutar(cs, "DROP TABLE IF EXISTS dbo.BenchRespaldoPrecios;");

    // --- Escenario 7: deshacer las ventas de prueba y devolver el stock
    public static void RevertirVentas(string cs, int maxIdVentaAnterior) =>
        Ejecutar(cs, @"
BEGIN TRANSACTION;
UPDATE p SET p.Stock = p.Stock + x.Cantidad
FROM dbo.Producto AS p
JOIN (SELECT IdProducto, SUM(Cantidad) AS Cantidad
      FROM dbo.DetalleVenta WHERE IdVenta > @max
      GROUP BY IdProducto) AS x ON x.IdProducto = p.IdProducto;
DELETE FROM dbo.DetalleVenta WHERE IdVenta > @max;
DELETE FROM dbo.Venta        WHERE IdVenta > @max;
COMMIT TRANSACTION;", ("@max", maxIdVentaAnterior));

    public static string VersionSqlServer(string cs)
    {
        using var cn = new SqlConnection(cs);
        using var cmd = new SqlCommand("SELECT @@VERSION;", cn);
        cn.Open();
        var version = (string)cmd.ExecuteScalar()!;
        return version.Split('\n')[0].Trim();
    }

    public static List<(string Tabla, int Filas)> ContarFilas(string cs)
    {
        const string sql = @"
SELECT 'Categoria', COUNT(*) FROM dbo.Categoria
UNION ALL SELECT 'Producto', COUNT(*) FROM dbo.Producto
UNION ALL SELECT 'Cliente', COUNT(*) FROM dbo.Cliente
UNION ALL SELECT 'Venta', COUNT(*) FROM dbo.Venta
UNION ALL SELECT 'DetalleVenta', COUNT(*) FROM dbo.DetalleVenta;";

        var lista = new List<(string, int)>();
        using var cn = new SqlConnection(cs);
        using var cmd = new SqlCommand(sql, cn);
        cn.Open();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
            lista.Add((dr.GetString(0), dr.GetInt32(1)));
        return lista;
    }

    // ------------------------------------------------------------------ auxiliares

    private static List<int> ListaEnteros(string cs, string sql)
    {
        var lista = new List<int>();
        using var cn = new SqlConnection(cs);
        using var cmd = new SqlCommand(sql, cn);
        cn.Open();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
            lista.Add(dr.GetInt32(0));
        return lista;
    }

    private static int Escalar(string cs, string sql)
    {
        using var cn = new SqlConnection(cs);
        using var cmd = new SqlCommand(sql, cn);
        cn.Open();
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void Ejecutar(string cs, string sql, params (string Nombre, int Valor)[] parametros)
    {
        using var cn = new SqlConnection(cs);
        using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 300 };
        foreach (var (nombre, valor) in parametros)
            cmd.Parameters.Add(nombre, SqlDbType.Int).Value = valor;
        cn.Open();
        cmd.ExecuteNonQuery();
    }
}
