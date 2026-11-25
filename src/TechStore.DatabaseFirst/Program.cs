using Microsoft.EntityFrameworkCore;
using TechStore.DatabaseFirst.Models;

// ---------------------------------------------------------------------
// Database First: el modelo de la carpeta Models fue GENERADO desde la
// base de datos existente TechStoreDB con Scaffold-DbContext (scaffold.ps1).
// Aquí solo se usa para ejecutar consultas de ejemplo.
//
// Uso:  dotnet run                      (usa la instancia local)
//       dotnet run -- "<cadena de conexión>"
// ---------------------------------------------------------------------

var cadena = args.Length > 0
    ? args[0]
    : "Server=localhost;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;";

var opciones = new DbContextOptionsBuilder<TechStoreDbFirstContext>()
    .UseSqlServer(cadena)
    .Options;

using var ctx = new TechStoreDbFirstContext(opciones);

Console.WriteLine("=== TechStore · Database First (modelo generado con Scaffold-DbContext) ===\n");

// Consulta 1: productos por categoría (navegación generada: IdCategoriaNavigation)
var porCategoria = ctx.Producto
    .AsNoTracking()
    .GroupBy(p => p.IdCategoriaNavigation.Nombre)
    .Select(g => new { Categoria = g.Key, Productos = g.Count(), PrecioPromedio = g.Average(p => p.Precio) })
    .OrderBy(x => x.Categoria)
    .ToList();

Console.WriteLine("Productos por categoría:");
foreach (var c in porCategoria)
    Console.WriteLine($"  {c.Categoria,-12} {c.Productos,7:N0} productos   precio promedio {c.PrecioPromedio,10:N2}");

// Consulta 2: los 5 clientes con más compras
var topClientes = ctx.Cliente
    .AsNoTracking()
    .Select(c => new { c.Nombre, Compras = c.Venta.Count, Total = c.Venta.Sum(v => v.Total) })
    .OrderByDescending(c => c.Total)
    .Take(5)
    .ToList();

Console.WriteLine("\nTop 5 clientes por monto comprado:");
foreach (var c in topClientes)
    Console.WriteLine($"  {c.Nombre,-20} {c.Compras,3} compras   {c.Total,12:N2}");

// SQL generado para la consulta 1
Console.WriteLine("\nSQL generado por EF para la consulta 1:");
Console.WriteLine(ctx.Producto
    .GroupBy(p => p.IdCategoriaNavigation.Nombre)
    .Select(g => new { Categoria = g.Key, Productos = g.Count(), PrecioPromedio = g.Average(p => p.Precio) })
    .ToQueryString());
