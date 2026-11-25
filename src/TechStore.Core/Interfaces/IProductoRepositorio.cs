using TechStore.Core.Entidades;

namespace TechStore.Core.Interfaces;

/// <summary>
/// Contrato común (Parte B). Los métodos originales se mantienen tal cual;
/// los marcados como "agregado" son extensiones permitidas por la práctica.
/// </summary>
public interface IProductoRepositorio
{
    List<Producto> ObtenerTodos();
    Producto? ObtenerPorId(int id);

    // Filtros combinados: texto en el nombre y, opcionalmente, categoría
    List<Producto> Buscar(string texto, int? idCategoria);

    int Agregar(Producto producto);                 // devuelve el nuevo Id
    bool Actualizar(Producto producto);
    bool Eliminar(int id);                          // false si el producto tiene ventas

    // Actualización masiva: sube o baja precios de una categoría
    int AjustarPrecios(int idCategoria, decimal porcentaje);

    List<ReporteCategoria> ReporteVentasPorCategoria(
        DateTime desde, DateTime hasta);

    // ---- Agregados ----

    /// <summary>Primeros N productos ordenados por Id (escenario 1: 1,000 / 10,000 / 50,000).</summary>
    List<Producto> ObtenerPrimeros(int cantidad);

    /// <summary>Inserción en bloque (escenario 4, extra). Devuelve cuántas filas insertó.</summary>
    int AgregarEnBloque(List<Producto> productos);

    /// <summary>Mensaje de la última operación que devolvió false (por ejemplo, Eliminar).</summary>
    string? UltimoMensaje { get; }
}
