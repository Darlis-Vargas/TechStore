namespace TechStore.Core.Excepciones;

/// <summary>
/// Se lanza cuando un producto no tiene stock suficiente. Ambas
/// implementaciones hacen Rollback antes de propagarla.
/// </summary>
public class StockInsuficienteException : Exception
{
    public int IdProducto { get; }
    public int Disponible { get; }
    public int Solicitado { get; }

    public StockInsuficienteException(int idProducto, int disponible, int solicitado)
        : base($"Stock insuficiente para el producto {idProducto}: " +
               $"disponible {disponible}, solicitado {solicitado}. Se hizo Rollback de la venta completa.")
    {
        IdProducto = idProducto;
        Disponible = disponible;
        Solicitado = solicitado;
    }
}
