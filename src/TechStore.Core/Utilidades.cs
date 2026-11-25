namespace TechStore.Core;

/// <summary>Reglas compartidas para que ambas implementaciones se comporten igual.</summary>
public static class Utilidades
{
    /// <summary>
    /// Convierte el texto del usuario en un patrón LIKE "%texto%",
    /// escapando los comodines de SQL Server (%, _ y [) con corchetes.
    /// El resultado siempre viaja como PARÁMETRO, nunca concatenado al SQL.
    /// </summary>
    public static string PatronContiene(string texto)
    {
        var escapado = texto.Trim()
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
        return "%" + escapado + "%";
    }

    /// <summary>
    /// Valida y normaliza el porcentaje de AjustarPrecios (máximo 2 decimales).
    /// Un porcentaje de -100 o menos dejaría precios en 0 o negativos (CHECK Precio > 0).
    /// </summary>
    public static decimal NormalizarPorcentaje(decimal porcentaje)
    {
        if (porcentaje <= -100m)
            throw new ArgumentOutOfRangeException(nameof(porcentaje),
                "El porcentaje debe ser mayor que -100 %.");
        return Math.Round(porcentaje, 2);
    }

    /// <summary>Une cantidades del mismo producto y valida los ítems de una venta.</summary>
    public static List<(int IdProducto, int Cantidad)> ConsolidarItems(
        List<(int IdProducto, int Cantidad)> items)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException("La venta debe tener al menos un producto.", nameof(items));
        if (items.Any(i => i.Cantidad <= 0))
            throw new ArgumentException("Todas las cantidades deben ser mayores que cero.", nameof(items));

        return items
            .GroupBy(i => i.IdProducto)
            .Select(g => (g.Key, g.Sum(i => i.Cantidad)))
            .ToList();
    }
}
