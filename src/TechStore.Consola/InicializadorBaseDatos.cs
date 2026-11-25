using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TechStore.EFCore;

namespace TechStore.Consola;

/// <summary>
/// Crea TechStoreDB_EF aplicando las migraciones de Code First y luego la llena
/// con el MISMO script de carga que usa TechStoreDB.sql (SeedData.sql).
/// </summary>
public static class InicializadorBaseDatos
{
    public static void InicializarEf(string cadenaEf, bool recrear)
    {
        using (var ctx = TechStoreContextFactory.Crear(cadenaEf))
        {
            if (recrear)
            {
                Console.WriteLine("Borrando TechStoreDB_EF...");
                ctx.Database.EnsureDeleted();
            }

            Console.WriteLine("Aplicando migraciones (Database.Migrate)...");
            ctx.Database.Migrate();   // crea la base si no existe y aplica las migraciones pendientes

            foreach (var m in ctx.Database.GetAppliedMigrations())
                Console.WriteLine($"  Migración aplicada: {m}");
        }

        var rutaScript = Path.Combine(AppContext.BaseDirectory, "SeedData.sql");
        if (!File.Exists(rutaScript))
            throw new FileNotFoundException("No se encontró SeedData.sql junto al ejecutable.", rutaScript);

        Console.WriteLine("Cargando datos de prueba (SeedData.sql)...");
        using var cn = new SqlConnection(cadenaEf);
        cn.InfoMessage += (_, e) => Console.WriteLine("  " + e.Message);
        cn.Open();
        using var cmd = new SqlCommand(File.ReadAllText(rutaScript), cn) { CommandTimeout = 600 };
        cmd.ExecuteNonQuery();

        Console.WriteLine("TechStoreDB_EF lista.");
    }
}
