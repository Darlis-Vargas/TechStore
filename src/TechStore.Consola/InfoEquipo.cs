using System.Runtime.InteropServices;
using System.Text;

namespace TechStore.Consola;

/// <summary>Características del equipo, requeridas en el informe (Parte E).</summary>
public static class InfoEquipo
{
    public static string Describir(string cadenaAdo, string cadenaEf)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Fecha de la prueba : {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Sistema operativo  : {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($"Procesador         : {NombreProcesador()}");
        sb.AppendLine($"Núcleos lógicos    : {Environment.ProcessorCount}");
        sb.AppendLine($"Memoria RAM        : {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024d / 1024 / 1024:N1} GB");
        sb.AppendLine($"Runtime .NET       : {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Compilación        : {Compilacion}");
        sb.AppendLine($"EF Core            : {typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.GetName().Version}");
        sb.AppendLine($"Microsoft.Data.SqlClient: {typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly.GetName().Version}");

        try { sb.AppendLine($"SQL Server         : {DatosPrueba.VersionSqlServer(cadenaAdo)}"); }
        catch (Exception ex) { sb.AppendLine($"SQL Server         : (no disponible: {ex.Message})"); }

        AgregarConteo(sb, "TechStoreDB", cadenaAdo);
        AgregarConteo(sb, "TechStoreDB_EF", cadenaEf);
        return sb.ToString();
    }

    public static string Compilacion =>
#if DEBUG
        "Debug  (¡ATENCIÓN! las mediciones válidas deben hacerse en Release)";
#else
        "Release";
#endif

    private static void AgregarConteo(StringBuilder sb, string nombre, string cadena)
    {
        try
        {
            var filas = DatosPrueba.ContarFilas(cadena);
            sb.AppendLine($"{nombre,-19}: " + string.Join(", ", filas.Select(f => $"{f.Tabla} {f.Filas:N0}")));
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{nombre,-19}: (no disponible: {ex.Message})");
        }
    }

    private static string NombreProcesador()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var clave = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (clave?.GetValue("ProcessorNameString") is string nombre)
                    return nombre.Trim();
            }
            if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            {
                var linea = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name"));
                if (linea is not null) return linea.Split(':', 2)[1].Trim();
            }
        }
        catch { /* se usa el valor de respaldo */ }

        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "(anótalo manualmente)";
    }
}
