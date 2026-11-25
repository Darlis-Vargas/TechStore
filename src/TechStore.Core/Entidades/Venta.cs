namespace TechStore.Core.Entidades;

public class Venta
{
    public int IdVenta { get; set; }
    public int IdCliente { get; set; }
    public DateTime Fecha { get; set; }   // DEFAULT GETDATE() en la base de datos
    public decimal Total { get; set; }

    public Cliente? Cliente { get; set; }
    public List<DetalleVenta> Detalles { get; set; } = new();
}
