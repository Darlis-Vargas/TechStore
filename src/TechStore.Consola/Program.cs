using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using TechStore.AdoNet;
using TechStore.Consola;
using TechStore.Core.Entidades;
using TechStore.Core.Excepciones;
using TechStore.Core.Interfaces;
using TechStore.EFCore;

Console.OutputEncoding = Encoding.UTF8;

// ------------------------------------------------------------------ Configuración
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

string csAdo = config.GetConnectionString("TechStoreDB")
    ?? throw new InvalidOperationException("Falta la cadena 'TechStoreDB' en appsettings.json");
string csEf = config.GetConnectionString("TechStoreDB_EF")
    ?? throw new InvalidOperationException("Falta la cadena 'TechStoreDB_EF' en appsettings.json");
int repeticiones = int.TryParse(config["Rendimiento:Repeticiones"], out var rep) ? rep : 5;

// ------------------------------------------------------------------ Implementaciones
var productoAdo = new ProductoRepositorioAdo(csAdo);
var ventaAdo = new VentaRepositorioAdo(csAdo);

Func<TechStoreContext> crearContexto = () => TechStoreContextFactory.Crear(csEf);
var productoEf = new ProductoRepositorioEf(crearContexto);
var ventaEf = new VentaRepositorioEf(crearContexto);

// El resto del programa solo conoce las INTERFACES: cambiar de tecnología
// no requiere tocar el código de las opciones del menú.
bool usandoEf = false;
IProductoRepositorio productos = productoAdo;
IVentaRepositorio ventas = ventaAdo;

string TecnologiaActual() => usandoEf ? "EF Core  (TechStoreDB_EF)" : "ADO.NET  (TechStoreDB)";
string CadenaActual() => usandoEf ? csEf : csAdo;

// ------------------------------------------------------------------ Menú
while (true)
{
    Console.WriteLine();
    Console.WriteLine("==============================================================");
    Console.WriteLine("  TechStore · ADO.NET vs. Entity Framework Core");
    Console.WriteLine($"  Tecnología activa: {TecnologiaActual()}");
    Console.WriteLine("==============================================================");
    Console.WriteLine("   1. Cambiar tecnología (ADO.NET <-> EF Core)");
    Console.WriteLine("   2. Listar productos (primeros 20)");
    Console.WriteLine("   3. Buscar productos (texto + categoría)");
    Console.WriteLine("   4. Ver producto por Id");
    Console.WriteLine("   5. Agregar producto");
    Console.WriteLine("   6. Actualizar producto");
    Console.WriteLine("   7. Eliminar producto");
    Console.WriteLine("   8. Ajustar precios de una categoría");
    Console.WriteLine("   9. Reporte de ventas por categoría");
    Console.WriteLine("  10. Registrar venta");
    Console.WriteLine("  11. DEMO: venta con Rollback por falta de stock");
    Console.WriteLine("  12. Pruebas de rendimiento");
    Console.WriteLine("  13. Ver SQL generado por EF Core (ToQueryString)");
    Console.WriteLine("  14. Inicializar TechStoreDB_EF (migraciones + datos)");
    Console.WriteLine("  15. Información del equipo y de las bases de datos");
    Console.WriteLine("   0. Salir");
    Console.Write("Opción: ");

    var opcion = Console.ReadLine()?.Trim();
    if (opcion == "0" || opcion is null) break;

    try
    {
        switch (opcion)
        {
            case "1": CambiarTecnologia(); break;
            case "2": Listar(); break;
            case "3": Buscar(); break;
            case "4": VerPorId(); break;
            case "5": Agregar(); break;
            case "6": Actualizar(); break;
            case "7": Eliminar(); break;
            case "8": AjustarPrecios(); break;
            case "9": Reporte(); break;
            case "10": RegistrarVenta(); break;
            case "11": DemoRollback(); break;
            case "12": PruebasDeRendimiento(); break;
            case "13": VerSqlEf(); break;
            case "14": InicializarEf(); break;
            case "15": Console.WriteLine("\n" + InfoEquipo.Describir(csAdo, csEf)); break;
            default: Console.WriteLine("Opción no válida."); break;
        }
    }
    catch (Exception ex)
    {
        // Ningún error cierra el programa: se muestra el mensaje y se vuelve al menú.
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nError: {MensajeRaiz(ex)}");
        Console.ResetColor();
    }
}

// ================================================================== Opciones

void CambiarTecnologia()
{
    usandoEf = !usandoEf;
    productos = usandoEf ? productoEf : productoAdo;
    ventas = usandoEf ? ventaEf : ventaAdo;
    Console.WriteLine($"Ahora se usa: {TecnologiaActual()}");
}

void Listar()
{
    var lista = productos.ObtenerPrimeros(20);
    ImprimirProductos(lista);
}

void Buscar()
{
    var texto = LeerTexto("Texto en el nombre (Enter = cualquiera): ", "");
    var categoria = LeerEnteroOpcional("Id de categoría (Enter = todas): ");
    var lista = productos.Buscar(texto, categoria);
    Console.WriteLine($"Se encontraron {lista.Count:N0} productos. Primeros 20:");
    ImprimirProductos(lista.Take(20));
}

void VerPorId()
{
    int id = LeerEntero("Id del producto: ");
    var p = productos.ObtenerPorId(id);
    Console.WriteLine(p is null ? "No existe." : p.ToString());
}

void Agregar()
{
    var p = new Producto
    {
        Codigo = LeerTexto("Código (máx. 20): "),
        Nombre = LeerTexto("Nombre: "),
        Precio = LeerDecimal("Precio: "),
        Stock = LeerEntero("Stock: "),
        IdCategoria = LeerEntero("Id de categoría (1-5): ")
    };
    int id = productos.Agregar(p);
    Console.WriteLine($"Producto agregado con Id {id}.");
}

void Actualizar()
{
    int id = LeerEntero("Id del producto a actualizar: ");
    var p = productos.ObtenerPorId(id);
    if (p is null) { Console.WriteLine("No existe."); return; }

    Console.WriteLine(p);
    Console.WriteLine("(Enter conserva el valor actual)");
    p.Codigo = LeerTexto($"Código [{p.Codigo}]: ", p.Codigo);
    p.Nombre = LeerTexto($"Nombre [{p.Nombre}]: ", p.Nombre);
    p.Precio = LeerDecimal($"Precio [{p.Precio}]: ", p.Precio);
    p.Stock = LeerEntero($"Stock [{p.Stock}]: ", p.Stock);
    p.IdCategoria = LeerEntero($"Categoría [{p.IdCategoria}]: ", p.IdCategoria);

    Console.WriteLine(productos.Actualizar(p) ? "Producto actualizado." : productos.UltimoMensaje);
}

void Eliminar()
{
    int id = LeerEntero("Id del producto a eliminar: ");
    if (productos.Eliminar(id))
        Console.WriteLine("Producto eliminado.");
    else
        Console.WriteLine(productos.UltimoMensaje);
}

void AjustarPrecios()
{
    int categoria = LeerEntero("Id de categoría: ");
    decimal porcentaje = LeerDecimal("Porcentaje (ej. 10 = +10 %, -5 = -5 %): ");
    int filas = productos.AjustarPrecios(categoria, porcentaje);
    Console.WriteLine($"{filas:N0} productos actualizados.");
}

void Reporte()
{
    var desde = LeerFecha("Desde (aaaa-mm-dd) [2025-01-01]: ", new DateTime(2025, 1, 1));
    var hasta = LeerFecha($"Hasta (aaaa-mm-dd) [{DateTime.Today:yyyy-MM-dd}]: ", DateTime.Today);
    hasta = hasta.Date.AddDays(1).AddTicks(-1);   // incluye todo el último día

    var reporte = productos.ReporteVentasPorCategoria(desde, hasta);
    Console.WriteLine($"\n{"Categoría",-15} {"Unidades",10} {"Total vendido",16}");
    foreach (var r in reporte)
        Console.WriteLine($"{r.Categoria,-15} {r.UnidadesVendidas,10:N0} {r.TotalVendido,16:N2}");
    Console.WriteLine($"{"TOTAL",-15} {reporte.Sum(r => r.UnidadesVendidas),10:N0} {reporte.Sum(r => r.TotalVendido),16:N2}");
}

void RegistrarVenta()
{
    int idCliente = LeerEntero("Id del cliente: ");
    var items = new List<(int IdProducto, int Cantidad)>();
    Console.WriteLine("Escribe 'IdProducto Cantidad' por línea (Enter vacío para terminar):");
    while (true)
    {
        var linea = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(linea)) break;
        var partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 2 && int.TryParse(partes[0], out var idP) && int.TryParse(partes[1], out var cant))
            items.Add((idP, cant));
        else
            Console.WriteLine("Formato inválido. Ejemplo: 25 2");
    }

    try
    {
        int idVenta = ventas.RegistrarVenta(idCliente, items);
        Console.WriteLine($"Venta {idVenta} registrada (Commit).");
    }
    catch (StockInsuficienteException ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(ex.Message);
        Console.ResetColor();
    }
}

void DemoRollback()
{
    Console.WriteLine($"\n--- Demostración de Rollback con {TecnologiaActual()} ---");
    var dos = productos.ObtenerPrimeros(2);
    if (dos.Count < 2) { Console.WriteLine("Se necesitan al menos 2 productos."); return; }
    var (a, b) = (dos[0], dos[1]);
    int idCliente = DatosPrueba.IdsCliente(CadenaActual()).First();

    int ventasAntes = ventas.ContarVentas();
    Console.WriteLine($"Antes:   ventas = {ventasAntes:N0} | stock {a.Codigo} = {a.Stock} | stock {b.Codigo} = {b.Stock}");
    Console.WriteLine($"Se intenta vender: 1 x {a.Codigo} (hay stock) y {b.Stock + 1} x {b.Codigo} (NO alcanza).");
    Console.WriteLine("El primer producto SÍ se descuenta dentro de la transacción; el segundo falla.");

    try
    {
        ventas.RegistrarVenta(idCliente, new List<(int, int)> { (a.IdProducto, 1), (b.IdProducto, b.Stock + 1) });
        Console.WriteLine("¡La venta se guardó! (no debería ocurrir)");
    }
    catch (StockInsuficienteException ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Excepción capturada: " + ex.Message);
        Console.ResetColor();
    }

    var a2 = productos.ObtenerPorId(a.IdProducto)!;
    var b2 = productos.ObtenerPorId(b.IdProducto)!;
    int ventasDespues = ventas.ContarVentas();
    Console.WriteLine($"Después: ventas = {ventasDespues:N0} | stock {a2.Codigo} = {a2.Stock} | stock {b2.Codigo} = {b2.Stock}");

    bool ok = ventasDespues == ventasAntes && a2.Stock == a.Stock && b2.Stock == b.Stock;
    Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine(ok
        ? "✔ Rollback correcto: no se guardó la venta, ni los detalles, ni el descuento de stock."
        : "✘ Algo quedó guardado: revisa la transacción.");
    Console.ResetColor();
}

void PruebasDeRendimiento()
{
    Console.WriteLine($"\nCompilación: {InfoEquipo.Compilacion}");
    var texto = LeerTexto("Escenarios a ejecutar (Enter = todos, ej. 1,2,7): ", "");
    var escenarios = texto
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => int.TryParse(s, out var n) ? n : 0)
        .Where(n => n is >= 1 and <= 7)
        .ToHashSet();

    var pruebas = new PruebasRendimiento(productoAdo, ventaAdo, productoEf, ventaEf, csAdo, csEf, repeticiones);
    pruebas.Ejecutar(escenarios);

    var carpeta = Path.Combine(CarpetaRepositorio(), "resultados");
    var (csv, md) = pruebas.GuardarResultados(carpeta, InfoEquipo.Describir(csAdo, csEf));
    Console.WriteLine($"\nResultados guardados en:\n  {csv}\n  {md}");
    Console.WriteLine("Para los gráficos del informe: python tools/graficos.py");
}

void VerSqlEf()
{
    var desde = new DateTime(2025, 1, 1);
    var hasta = new DateTime(2026, 12, 31);
    Console.WriteLine("\n-- ReporteVentasPorCategoria (LINQ GroupBy) --");
    Console.WriteLine(productoEf.ObtenerSqlReporte(desde, hasta));
    Console.WriteLine("\n-- Buscar(\"Dell Pro\", 1) --");
    Console.WriteLine(productoEf.ObtenerSqlBuscar("Dell Pro", 1));
}

void InicializarEf()
{
    var recrear = LeerTexto("¿Borrar y recrear TechStoreDB_EF desde cero? (s/N): ", "n")
        .StartsWith("s", StringComparison.OrdinalIgnoreCase);
    InicializadorBaseDatos.InicializarEf(csEf, recrear);
}

// ================================================================== Utilidades de consola

static void ImprimirProductos(IEnumerable<Producto> lista)
{
    Console.WriteLine($"{"Id",6} | {"Código",-8} | {"Nombre",-38} | {"Precio",10} | {"Stock",11} | Categoría");
    foreach (var p in lista) Console.WriteLine(p);
}

static string LeerTexto(string mensaje, string? porDefecto = null)
{
    while (true)
    {
        Console.Write(mensaje);
        var valor = Console.ReadLine()?.Trim() ?? "";
        if (valor.Length > 0) return valor;
        if (porDefecto is not null) return porDefecto;
        Console.WriteLine("El valor es obligatorio.");
    }
}

static int LeerEntero(string mensaje, int? porDefecto = null)
{
    while (true)
    {
        var texto = LeerTexto(mensaje, porDefecto?.ToString(CultureInfo.InvariantCulture));
        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        Console.WriteLine("Escribe un número entero.");
    }
}

static int? LeerEnteroOpcional(string mensaje)
{
    while (true)
    {
        var texto = LeerTexto(mensaje, "");
        if (texto.Length == 0) return null;
        if (int.TryParse(texto, out var n)) return n;
        Console.WriteLine("Escribe un número entero o deja vacío.");
    }
}

static decimal LeerDecimal(string mensaje, decimal? porDefecto = null)
{
    while (true)
    {
        var texto = LeerTexto(mensaje, porDefecto?.ToString(CultureInfo.InvariantCulture)).Replace(',', '.');
        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        Console.WriteLine("Escribe un número (ej. 1499.99).");
    }
}

static DateTime LeerFecha(string mensaje, DateTime porDefecto)
{
    while (true)
    {
        var texto = LeerTexto(mensaje, porDefecto.ToString("yyyy-MM-dd"));
        if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            return f;
        Console.WriteLine("Formato: aaaa-mm-dd");
    }
}

static string MensajeRaiz(Exception ex)
{
    while (ex.InnerException is not null) ex = ex.InnerException;
    return ex.Message;
}

static string CarpetaRepositorio()
{
    // Sube desde bin/Release/net8.0 hasta encontrar TechStore.sln
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TechStore.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
