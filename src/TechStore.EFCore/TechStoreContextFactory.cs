using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TechStore.EFCore;

/// <summary>
/// Fábrica de diseño: la usan los comandos "dotnet ef migrations ..." y
/// "dotnet ef database update" para crear el contexto sin ejecutar la consola.
/// La cadena se toma de la variable de entorno TECHSTORE_EF; si no existe,
/// se usa la instancia local por defecto.
/// </summary>
public class TechStoreContextFactory : IDesignTimeDbContextFactory<TechStoreContext>
{
    public const string CadenaPorDefecto =
        "Server=localhost;Database=TechStoreDB_EF;Trusted_Connection=True;TrustServerCertificate=True;";

    public TechStoreContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("TECHSTORE_EF") ?? CadenaPorDefecto;
        return Crear(cadena);
    }

    public static TechStoreContext Crear(string cadenaConexion)
    {
        var opciones = new DbContextOptionsBuilder<TechStoreContext>()
            .UseSqlServer(cadenaConexion, sql => sql.CommandTimeout(300))
            .Options;
        return new TechStoreContext(opciones);
    }
}
