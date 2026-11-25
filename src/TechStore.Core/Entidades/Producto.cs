namespace TechStore.Core.Entidades;

public class Producto
{
    public int IdProducto { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int IdCategoria { get; set; }
    public string? Marca { get; set; }

    // Navegación (la usa EF Core; ADO.NET la ignora)
    public Categoria? Categoria { get; set; }
    public List<DetalleVenta> Detalles { get; set; } = new();

    public override string ToString() =>
        $"{IdProducto,6} | {Codigo,-8} | {Nombre,-38} | {Precio,10:N2} | Stock {Stock,5} | Cat {IdCategoria}";
}
