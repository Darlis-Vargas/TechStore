using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using TechStore.Core;
using TechStore.Core.Entidades;
using TechStore.Core.Interfaces;

namespace TechStore.AdoNet;

/// <summary>
/// Implementación con ADO.NET puro (modo conectado):
/// SqlConnection + SqlCommand + SqlDataReader.
/// TODAS las consultas usan parámetros (@...). Nunca se concatena texto del usuario.
/// Cada método abre y cierra su conexión; el pool de conexiones de
/// Microsoft.Data.SqlClient reutiliza las conexiones físicas.
/// </summary>
public class ProductoRepositorioAdo : IProductoRepositorio
{
    private readonly string _cadenaConexion;

    // Columnas en un orden fijo: Mapear() lee por posición (0..5), que es lo más rápido.
    private const string SelectBase =
        "SELECT IdProducto, Codigo, Nombre, Precio, Stock, IdCategoria, Marca FROM dbo.Producto";

    public string? UltimoMensaje { get; private set; }

    public ProductoRepositorioAdo(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    // ------------------------------------------------------------------ Lectura

    public List<Producto> ObtenerTodos() =>
        Consultar(SelectBase + " ORDER BY IdProducto;");

    public List<Producto> ObtenerPrimeros(int cantidad) =>
        Consultar(
            "SELECT TOP (@cantidad) IdProducto, Codigo, Nombre, Precio, Stock, IdCategoria, Marca " +
            "FROM dbo.Producto ORDER BY IdProducto;",
            cmd => cmd.Parameters.Add("@cantidad", SqlDbType.Int).Value = cantidad);

    public Producto? ObtenerPorId(int id) =>
        Consultar(
            SelectBase + " WHERE IdProducto = @id;",
            cmd => cmd.Parameters.Add("@id", SqlDbType.Int).Value = id)
        .FirstOrDefault();

    /// <summary>
    /// Filtro dinámico: solo se agregan las condiciones de los parámetros recibidos.
    /// Lo que se concatena son fragmentos FIJOS escritos por el programador
    /// (" AND Nombre LIKE @texto"); el valor del usuario va siempre en un parámetro.
    /// </summary>
    public List<Producto> Buscar(string texto, int? idCategoria)
    {
        var sql = new StringBuilder(SelectBase).Append(" WHERE 1 = 1");
        var parametros = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            sql.Append(" AND Nombre LIKE @texto");
            parametros.Add(new SqlParameter("@texto", SqlDbType.NVarChar, 400)
            {
                Value = Utilidades.PatronContiene(texto)
            });
        }

        if (idCategoria.HasValue)
        {
            sql.Append(" AND IdCategoria = @idCategoria");
            parametros.Add(new SqlParameter("@idCategoria", SqlDbType.Int) { Value = idCategoria.Value });
        }

        sql.Append(" ORDER BY IdProducto;");

        return Consultar(sql.ToString(), cmd => cmd.Parameters.AddRange(parametros.ToArray()));
    }

    // ------------------------------------------------------------------ Escritura

    public int Agregar(Producto producto)
    {
        const string sql =
            "INSERT INTO dbo.Producto (Codigo, Nombre, Precio, Stock, IdCategoria, Marca) " +
            "VALUES (@codigo, @nombre, @precio, @stock, @idCategoria, @marca); " +
            "SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var cn = new SqlConnection(_cadenaConexion);
        using var cmd = new SqlCommand(sql, cn);
        AgregarParametrosProducto(cmd, producto);

        cn.Open();
        int nuevoId = (int)cmd.ExecuteScalar()!;
        producto.IdProducto = nuevoId;
        return nuevoId;
    }

    /// <summary>
    /// Inserción en bloque con SqlBulkCopy: envía todas las filas en un
    /// flujo binario (TDS bulk load) en lugar de un INSERT por fila.
    /// </summary>
    public int AgregarEnBloque(List<Producto> productos)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Codigo", typeof(string));
        tabla.Columns.Add("Nombre", typeof(string));
        tabla.Columns.Add("Precio", typeof(decimal));
        tabla.Columns.Add("Stock", typeof(int));
        tabla.Columns.Add("IdCategoria", typeof(int));
        tabla.Columns.Add("Marca", typeof(string));

        foreach (var p in productos)
            tabla.Rows.Add(p.Codigo, p.Nombre, p.Precio, p.Stock, p.IdCategoria, (object?)p.Marca ?? DBNull.Value);

        using var cn = new SqlConnection(_cadenaConexion);
        cn.Open();
        using var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.CheckConstraints, null)
        {
            DestinationTableName = "dbo.Producto",
            BatchSize = 5000
        };
        foreach (DataColumn col in tabla.Columns)
            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);

        bulk.WriteToServer(tabla);
        return tabla.Rows.Count;
    }

    public bool Actualizar(Producto producto)
    {
        const string sql =
            "UPDATE dbo.Producto SET Codigo = @codigo, Nombre = @nombre, Precio = @precio, " +
            "Stock = @stock, IdCategoria = @idCategoria, Marca = @marca WHERE IdProducto = @id;";

        using var cn = new SqlConnection(_cadenaConexion);
        using var cmd = new SqlCommand(sql, cn);
        AgregarParametrosProducto(cmd, producto);
        cmd.Parameters.Add("@id", SqlDbType.Int).Value = producto.IdProducto;

        cn.Open();
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Devuelve false (con mensaje) si el producto no existe o tiene ventas.
    /// Se verifica antes de borrar y, además, se captura el error 547 (violación
    /// de FK) por si otra venta se registra entre la verificación y el DELETE.
    /// </summary>
    public bool Eliminar(int id)
    {
        UltimoMensaje = null;
        const string sql =
            "IF EXISTS (SELECT 1 FROM dbo.DetalleVenta WHERE IdProducto = @id) " +
            "    SELECT -1; " +
            "ELSE " +
            "BEGIN " +
            "    DELETE FROM dbo.Producto WHERE IdProducto = @id; " +
            "    SELECT @@ROWCOUNT; " +
            "END";

        try
        {
            using var cn = new SqlConnection(_cadenaConexion);
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
            cn.Open();

            int resultado = (int)cmd.ExecuteScalar()!;
            if (resultado == -1)
            {
                UltimoMensaje = $"No se puede eliminar el producto {id}: tiene ventas registradas.";
                return false;
            }
            if (resultado == 0)
            {
                UltimoMensaje = $"No existe un producto con Id {id}.";
                return false;
            }
            return true;
        }
        catch (SqlException ex) when (ex.Number == 547)   // conflicto con una FK
        {
            UltimoMensaje = $"No se puede eliminar el producto {id}: tiene ventas registradas.";
            return false;
        }
    }

    /// <summary>Un único UPDATE basado en conjuntos: el trabajo lo hace SQL Server.</summary>
    public int AjustarPrecios(int idCategoria, decimal porcentaje)
    {
        porcentaje = Utilidades.NormalizarPorcentaje(porcentaje);
        const string sql =
            "UPDATE dbo.Producto " +
            "SET Precio = ROUND(Precio * (100 + @porcentaje) / 100, 2) " +
            "WHERE IdCategoria = @idCategoria;";

        using var cn = new SqlConnection(_cadenaConexion);
        using var cmd = new SqlCommand(sql, cn);
        var pPorc = cmd.Parameters.Add("@porcentaje", SqlDbType.Decimal);
        pPorc.Precision = 6;
        pPorc.Scale = 2;
        pPorc.Value = porcentaje;
        cmd.Parameters.Add("@idCategoria", SqlDbType.Int).Value = idCategoria;

        cn.Open();
        return cmd.ExecuteNonQuery();
    }

    // ------------------------------------------------------------------ Reporte

    /// <summary>Resuelto completamente en SQL con JOIN y GROUP BY.</summary>
    public List<ReporteCategoria> ReporteVentasPorCategoria(DateTime desde, DateTime hasta)
    {
        const string sql = @"
SELECT c.Nombre                          AS Categoria,
       SUM(d.Cantidad)                   AS UnidadesVendidas,
       SUM(d.Cantidad * d.PrecioUnitario) AS TotalVendido
FROM dbo.DetalleVenta AS d
JOIN dbo.Venta     AS v ON v.IdVenta     = d.IdVenta
JOIN dbo.Producto  AS p ON p.IdProducto  = d.IdProducto
JOIN dbo.Categoria AS c ON c.IdCategoria = p.IdCategoria
WHERE v.Fecha >= @desde AND v.Fecha <= @hasta
GROUP BY c.Nombre
ORDER BY TotalVendido DESC;";

        var reporte = new List<ReporteCategoria>();

        using var cn = new SqlConnection(_cadenaConexion);
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@desde", SqlDbType.DateTime2).Value = desde;
        cmd.Parameters.Add("@hasta", SqlDbType.DateTime2).Value = hasta;

        cn.Open();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
        {
            reporte.Add(new ReporteCategoria
            {
                Categoria = dr.GetString(0),
                UnidadesVendidas = dr.GetInt32(1),
                TotalVendido = dr.GetDecimal(2)
            });
        }
        return reporte;
    }

    // ------------------------------------------------------------------ Auxiliares

    private List<Producto> Consultar(string sql, Action<SqlCommand>? configurar = null)
    {
        var lista = new List<Producto>();

        using var cn = new SqlConnection(_cadenaConexion);
        using var cmd = new SqlCommand(sql, cn);
        configurar?.Invoke(cmd);

        cn.Open();
        using var dr = cmd.ExecuteReader();
        while (dr.Read())
            lista.Add(Mapear(dr));

        return lista;
    }

    // Conversión manual fila -> objeto: es el "trabajo extra" de ADO.NET que EF hace solo.
    private static Producto Mapear(SqlDataReader dr) => new()
    {
        IdProducto = dr.GetInt32(0),
        Codigo = dr.GetString(1),
        Nombre = dr.GetString(2),
        Precio = dr.GetDecimal(3),
        Stock = dr.GetInt32(4),
        IdCategoria = dr.GetInt32(5),
        Marca = dr.IsDBNull(6) ? null : dr.GetString(6)
    };

    private static void AgregarParametrosProducto(SqlCommand cmd, Producto p)
    {
        cmd.Parameters.Add("@codigo", SqlDbType.NVarChar, 20).Value = p.Codigo;
        cmd.Parameters.Add("@nombre", SqlDbType.NVarChar, 100).Value = p.Nombre;
        var precio = cmd.Parameters.Add("@precio", SqlDbType.Decimal);
        precio.Precision = 10;
        precio.Scale = 2;
        precio.Value = p.Precio;
        cmd.Parameters.Add("@stock", SqlDbType.Int).Value = p.Stock;
        cmd.Parameters.Add("@idCategoria", SqlDbType.Int).Value = p.IdCategoria;
        cmd.Parameters.Add("@marca", SqlDbType.NVarChar, 50).Value = (object?)p.Marca ?? DBNull.Value;
    }
}
