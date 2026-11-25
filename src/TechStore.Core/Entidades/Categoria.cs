namespace TechStore.Core.Entidades;

public class Categoria
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = "";

    // Navegación (la usa EF Core; ADO.NET la ignora)
    public List<Producto> Productos { get; set; } = new();
}
