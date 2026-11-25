using System.Diagnostics;
using System.Globalization;
using System.Text;
using TechStore.AdoNet;
using TechStore.Core.Entidades;
using TechStore.Core.Interfaces;
using TechStore.EFCore;

namespace TechStore.Consola;

public sealed record Resultado(
    int Escenario, string Prueba, string Tecnologia, string Variante,
    double PromedioMs, double MinMs, double MaxMs, int Mediciones);

/// <summary>
/// Parte E. Metodología:
///  - Stopwatch; compilar y ejecutar en Release (dotnet run -c Release).
///  - Cada escenario se ejecuta N veces (5 por defecto); la primera es de
///    calentamiento (JIT, pool de conexiones, caché de planes de SQL Server,
///    compilación del modelo de EF) y se descarta. Se reporta el promedio.
///  - Preparación y limpieza de datos se hacen FUERA del cronómetro, para que
///    cada repetición parta exactamente del mismo estado.
///  - ADO.NET trabaja sobre TechStoreDB y EF Core sobre TechStoreDB_EF; ambas
///    tienen los mismos datos porque se cargan con el mismo script determinista.
///  - Los valores aleatorios (Ids, ventas) usan una semilla fija: ambas
///    tecnologías reciben exactamente la misma secuencia de trabajo.
/// </summary>
public class PruebasRendimiento
{
    private readonly ProductoRepositorioAdo _prodAdo;
    private readonly VentaRepositorioAdo _ventaAdo;
    private readonly ProductoRepositorioEf _prodEf;
    private readonly VentaRepositorioEf _ventaEf;
    private readonly string _csAdo;
    private readonly string _csEf;
    private readonly int _repeticiones;
    private readonly List<Resultado> _resultados = new();

    public IReadOnlyList<Resultado> Resultados => _resultados;

    public PruebasRendimiento(
        ProductoRepositorioAdo prodAdo, VentaRepositorioAdo ventaAdo,
        ProductoRepositorioEf prodEf, VentaRepositorioEf ventaEf,
        string csAdo, string csEf, int repeticiones = 5)
    {
        if (repeticiones < 2)
            throw new ArgumentOutOfRangeException(nameof(repeticiones), "Se necesitan al menos 2 repeticiones (la primera se descarta).");

        _prodAdo = prodAdo;
        _ventaAdo = ventaAdo;
        _prodEf = prodEf;
        _ventaEf = ventaEf;
        _csAdo = csAdo;
        _csEf = csEf;
        _repeticiones = repeticiones;
    }

    /// <summary>
    /// Igual al método Medir de la guía, con dos mejoras: usa
    /// Elapsed.TotalMilliseconds (resolución de microsegundos) y permite pasos
    /// de preparación/limpieza que no se cronometran.
    /// </summary>
    public static (double Promedio, double Min, double Max) Medir(
        Action accion, int repeticiones = 5, Action? antes = null, Action? despues = null)
    {
        var tiempos = new List<double>();
        for (int i = 0; i < repeticiones; i++)
        {
            antes?.Invoke();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var reloj = Stopwatch.StartNew();
            accion();
            reloj.Stop();

            despues?.Invoke();

            if (i > 0)                              // descarta la primera (calentamiento)
                tiempos.Add(reloj.Elapsed.TotalMilliseconds);
        }
        return (tiempos.Average(), tiempos.Min(), tiempos.Max());
    }

    // =====================================================================
    public void Ejecutar(ISet<int>? escenarios = null)
    {
        bool Incluir(int n) => escenarios is null || escenarios.Count == 0 || escenarios.Contains(n);

        VerificarDatos();
        Console.WriteLine($"\nRepeticiones por prueba: {_repeticiones} (se descarta la primera). Compilación: {InfoEquipo.Compilacion}\n");
        Console.WriteLine($"{"#",-2} {"Prueba",-36} {"Tecnología",-9} {"Variante",-16} {"Prom (ms)",11} {"Mín",10} {"Máx",10}");
        Console.WriteLine(new string('-', 100));

        if (Incluir(1)) Escenario1_LeerTodos();
        if (Incluir(2)) Escenario2_BuscarPorId();
        if (Incluir(3)) Escenario3_BuscarConFiltros();
        if (Incluir(4)) Escenario4_Insertar();
        if (Incluir(5)) Escenario5_ActualizacionMasiva();
        if (Incluir(6)) Escenario6_Reporte();
        if (Incluir(7)) Escenario7_VentaTransaccional();

        Console.WriteLine(new string('-', 100));
        ImprimirResumen();
    }

    // ---------------------------------------------------------------- 1
    private void Escenario1_LeerTodos()
    {
        foreach (var n in new[] { 1_000, 10_000, 50_000 })
        {
            var prueba = $"Leer {n:N0} productos";
            // Con 50,000 se usa ObtenerTodos() (método del contrato); con menos, ObtenerPrimeros(n).
            Func<IProductoRepositorio, List<Producto>> leer =
                n == 50_000 ? r => r.ObtenerTodos() : r => r.ObtenerPrimeros(n);

            Registrar(1, prueba, "ADO.NET", "DataReader", () => leer(_prodAdo));

            _prodEf.UsarAsNoTracking = true;
            Registrar(1, prueba, "EF Core", "AsNoTracking", () => leer(_prodEf));

            _prodEf.UsarAsNoTracking = false;
            Registrar(1, prueba, "EF Core", "Con tracking", () => leer(_prodEf));
            _prodEf.UsarAsNoTracking = true;
        }
    }

    // ---------------------------------------------------------------- 2
    private void Escenario2_BuscarPorId()
    {
        var idsAdo = DatosPrueba.IdsProducto(_csAdo);
        var idsEf = DatosPrueba.IdsProducto(_csEf);
        int total = Math.Min(idsAdo.Count, idsEf.Count);

        var rnd = new Random(2026);
        var posiciones = Enumerable.Range(0, 1_000).Select(_ => rnd.Next(total)).ToArray();

        Registrar(2, "1,000 búsquedas por Id aleatorio", "ADO.NET", "", () =>
        {
            foreach (var pos in posiciones) _prodAdo.ObtenerPorId(idsAdo[pos]);
        });

        Registrar(2, "1,000 búsquedas por Id aleatorio", "EF Core", "AsNoTracking", () =>
        {
            foreach (var pos in posiciones) _prodEf.ObtenerPorId(idsEf[pos]);
        });
    }

    // ---------------------------------------------------------------- 3
    private void Escenario3_BuscarConFiltros()
    {
        const string texto = "Dell Pro";
        int catAdo = DatosPrueba.PrimeraCategoria(_csAdo);
        int catEf = DatosPrueba.PrimeraCategoria(_csEf);
        var prueba = $"100 búsquedas \"{texto}\" + categoría";

        Registrar(3, prueba, "ADO.NET", "SQL dinámico", () =>
        {
            for (int i = 0; i < 100; i++) _prodAdo.Buscar(texto, catAdo);
        });

        Registrar(3, prueba, "EF Core", "LINQ dinámico", () =>
        {
            for (int i = 0; i < 100; i++) _prodEf.Buscar(texto, catEf);
        });
    }

    // ---------------------------------------------------------------- 4
    private void Escenario4_Insertar()
    {
        const int cantidad = 1_000;
        int catAdo = DatosPrueba.PrimeraCategoria(_csAdo);
        int catEf = DatosPrueba.PrimeraCategoria(_csEf);

        // Por si una ejecución anterior se interrumpió
        DatosPrueba.EliminarProductosPrueba(_csAdo);
        DatosPrueba.EliminarProductosPrueba(_csEf);

        List<Producto> lote = new();

        // Uno a uno
        Registrar(4, "Insertar 1,000 productos uno a uno", "ADO.NET", "INSERT x1000",
            () => { foreach (var p in lote) _prodAdo.Agregar(p); },
            antes: () => lote = GenerarProductos(cantidad, catAdo),
            despues: () => DatosPrueba.EliminarProductosPrueba(_csAdo));

        Registrar(4, "Insertar 1,000 productos uno a uno", "EF Core", "SaveChanges x1000",
            () => { foreach (var p in lote) _prodEf.Agregar(p); },
            antes: () => lote = GenerarProductos(cantidad, catEf),
            despues: () => DatosPrueba.EliminarProductosPrueba(_csEf));

        // En bloque (extra)
        Registrar(4, "Insertar 1,000 productos en bloque", "ADO.NET", "SqlBulkCopy",
            () => _prodAdo.AgregarEnBloque(lote),
            antes: () => lote = GenerarProductos(cantidad, catAdo),
            despues: () => DatosPrueba.EliminarProductosPrueba(_csAdo));

        Registrar(4, "Insertar 1,000 productos en bloque", "EF Core", "AddRange+SaveChanges",
            () => _prodEf.AgregarEnBloque(lote),
            antes: () => lote = GenerarProductos(cantidad, catEf),
            despues: () => DatosPrueba.EliminarProductosPrueba(_csEf));
    }

    private static List<Producto> GenerarProductos(int cantidad, int idCategoria) =>
        Enumerable.Range(1, cantidad).Select(i => new Producto
        {
            Codigo = $"{DatosPrueba.PrefijoCodigoPrueba}{i:D6}",
            Nombre = $"Producto de prueba {i}",
            Precio = 100m + i % 50,
            Stock = 10,
            IdCategoria = idCategoria
        }).ToList();

    // ---------------------------------------------------------------- 5
    private void Escenario5_ActualizacionMasiva()
    {
        const decimal porcentaje = 5m;
        int catAdo = DatosPrueba.PrimeraCategoria(_csAdo);
        int catEf = DatosPrueba.PrimeraCategoria(_csEf);

        DatosPrueba.RespaldarPrecios(_csAdo, catAdo);
        DatosPrueba.RespaldarPrecios(_csEf, catEf);
        try
        {
            Registrar(5, "AjustarPrecios +5 % (categoría completa)", "ADO.NET", "UPDATE ... WHERE",
                () => _prodAdo.AjustarPrecios(catAdo, porcentaje),
                despues: () => DatosPrueba.RestaurarPrecios(_csAdo));

            Registrar(5, "AjustarPrecios +5 % (categoría completa)", "EF Core", "Entidades+SaveChanges",
                () => _prodEf.AjustarPreciosConEntidades(catEf, porcentaje),
                despues: () => DatosPrueba.RestaurarPrecios(_csEf));

            Registrar(5, "AjustarPrecios +5 % (categoría completa)", "EF Core", "ExecuteUpdate",
                () => _prodEf.AjustarPreciosConExecuteUpdate(catEf, porcentaje),
                despues: () => DatosPrueba.RestaurarPrecios(_csEf));
        }
        finally
        {
            DatosPrueba.EliminarRespaldoPrecios(_csAdo);
            DatosPrueba.EliminarRespaldoPrecios(_csEf);
        }
    }

    // ---------------------------------------------------------------- 6
    private void Escenario6_Reporte()
    {
        var desde = new DateTime(2025, 1, 1);
        var hasta = new DateTime(2026, 12, 31, 23, 59, 59);

        Registrar(6, "Reporte ventas por categoría", "ADO.NET", "JOIN + GROUP BY",
            () => _prodAdo.ReporteVentasPorCategoria(desde, hasta));

        Registrar(6, "Reporte ventas por categoría", "EF Core", "LINQ GroupBy",
            () => _prodEf.ReporteVentasPorCategoria(desde, hasta));
    }

    // ---------------------------------------------------------------- 7
    private void Escenario7_VentaTransaccional()
    {
        var prodAdo = DatosPrueba.IdsProducto(_csAdo);
        var prodEf = DatosPrueba.IdsProducto(_csEf);
        var cliAdo = DatosPrueba.IdsCliente(_csAdo);
        var cliEf = DatosPrueba.IdsCliente(_csEf);
        int nProd = Math.Min(prodAdo.Count, prodEf.Count);
        int nCli = Math.Min(cliAdo.Count, cliEf.Count);

        // Plan fijo: 100 ventas, cada una con 3 productos distintos y cantidad 1.
        var rnd = new Random(7);
        var plan = Enumerable.Range(0, 100).Select(_ =>
        {
            var posiciones = new HashSet<int>();
            while (posiciones.Count < 3) posiciones.Add(rnd.Next(nProd));
            return (Cliente: rnd.Next(nCli), Productos: posiciones.ToArray());
        }).ToList();

        int maxAdo = 0, maxEf = 0;

        Registrar(7, "100 ventas de 3 productos", "ADO.NET", "SqlTransaction",
            () =>
            {
                foreach (var v in plan)
                    _ventaAdo.RegistrarVenta(cliAdo[v.Cliente],
                        v.Productos.Select(p => (prodAdo[p], 1)).ToList());
            },
            antes: () => maxAdo = DatosPrueba.MaxIdVenta(_csAdo),
            despues: () => DatosPrueba.RevertirVentas(_csAdo, maxAdo));

        Registrar(7, "100 ventas de 3 productos", "EF Core", "BeginTransaction",
            () =>
            {
                foreach (var v in plan)
                    _ventaEf.RegistrarVenta(cliEf[v.Cliente],
                        v.Productos.Select(p => (prodEf[p], 1)).ToList());
            },
            antes: () => maxEf = DatosPrueba.MaxIdVenta(_csEf),
            despues: () => DatosPrueba.RevertirVentas(_csEf, maxEf));
    }

    // =====================================================================

    private void Registrar(int escenario, string prueba, string tecnologia, string variante,
        Action accion, Action? antes = null, Action? despues = null)
    {
        Console.Write($"{escenario,-2} {Recortar(prueba, 36),-36} {tecnologia,-9} {Recortar(variante, 16),-16} ");
        var (prom, min, max) = Medir(accion, _repeticiones, antes, despues);
        Console.WriteLine($"{prom,11:N2} {min,10:N2} {max,10:N2}");
        _resultados.Add(new Resultado(escenario, prueba, tecnologia, variante, prom, min, max, _repeticiones - 1));
    }

    private void VerificarDatos()
    {
        int ado = DatosPrueba.ContarProductos(_csAdo);
        int ef = DatosPrueba.ContarProductos(_csEf);
        Console.WriteLine($"Productos en TechStoreDB: {ado:N0}   ·   en TechStoreDB_EF: {ef:N0}");
        if (ado < 50_000 || ef < 50_000)
            throw new InvalidOperationException(
                "Ambas bases deben tener al menos 50,000 productos. Ejecuta TechStoreDB.sql y la opción 'Inicializar TechStoreDB_EF'.");
        if (ado != ef)
            Console.WriteLine("ADVERTENCIA: las bases no tienen el mismo número de productos; la comparación no sería justa.");
    }

    private void ImprimirResumen()
    {
        Console.WriteLine("\nRelación EF Core / ADO.NET (1.00 = igual; >1 = EF más lento):");
        foreach (var grupo in _resultados.GroupBy(r => (r.Escenario, r.Prueba)))
        {
            var ado = grupo.FirstOrDefault(r => r.Tecnologia == "ADO.NET");
            if (ado is null || ado.PromedioMs <= 0) continue;
            foreach (var ef in grupo.Where(r => r.Tecnologia == "EF Core"))
                Console.WriteLine($"  {grupo.Key.Prueba,-40} {ef.Variante,-22} x{ef.PromedioMs / ado.PromedioMs:N2}");
        }
    }

    /// <summary>Guarda CSV (para graficar) y Markdown (para pegar en el informe).</summary>
    public (string Csv, string Markdown) GuardarResultados(string carpeta, string infoEquipo)
    {
        Directory.CreateDirectory(carpeta);
        var sello = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var inv = CultureInfo.InvariantCulture;

        var csv = new StringBuilder("Escenario,Prueba,Tecnologia,Variante,PromedioMs,MinMs,MaxMs,Mediciones\n");
        foreach (var r in _resultados)
            csv.AppendLine(string.Join(",",
                r.Escenario.ToString(inv), Csv(r.Prueba), Csv(r.Tecnologia), Csv(r.Variante),
                r.PromedioMs.ToString("F3", inv), r.MinMs.ToString("F3", inv),
                r.MaxMs.ToString("F3", inv), r.Mediciones.ToString(inv)));

        var md = new StringBuilder();
        md.AppendLine($"# Resultados de rendimiento — {DateTime.Now:yyyy-MM-dd HH:mm}");
        md.AppendLine();
        md.AppendLine("```");
        md.Append(infoEquipo);
        md.AppendLine("```");
        md.AppendLine();
        md.AppendLine($"Cada prueba se ejecutó {_repeticiones} veces; se descartó la primera y se promediaron las {_repeticiones - 1} restantes.");
        md.AppendLine();
        md.AppendLine("| # | Prueba | Tecnología | Variante | Promedio (ms) | Mín (ms) | Máx (ms) |");
        md.AppendLine("|---|---|---|---|---:|---:|---:|");
        foreach (var r in _resultados)
            md.AppendLine($"| {r.Escenario} | {r.Prueba} | {r.Tecnologia} | {r.Variante} | " +
                          $"{r.PromedioMs.ToString("N2", inv)} | {r.MinMs.ToString("N2", inv)} | {r.MaxMs.ToString("N2", inv)} |");

        var rutaCsv = Path.Combine(carpeta, $"rendimiento_{sello}.csv");
        var rutaMd = Path.Combine(carpeta, $"rendimiento_{sello}.md");
        File.WriteAllText(rutaCsv, csv.ToString(), new UTF8Encoding(false));
        File.WriteAllText(rutaMd, md.ToString(), new UTF8Encoding(false));
        return (rutaCsv, rutaMd);
    }

    private static string Csv(string valor) => "\"" + valor.Replace("\"", "\"\"") + "\"";

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
