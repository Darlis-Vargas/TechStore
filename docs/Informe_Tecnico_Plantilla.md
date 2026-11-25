# Evaluación comparativa entre ADO.NET y Entity Framework Core
### Caso TechStore: inventario y ventas

**Autor(es):** ______________________ · **Asignatura:** ______________________ · **Fecha:** ____________
**Repositorio:** https://github.com/______/TechStore

> **Cómo usar esta plantilla.** Las secciones 1 y 2 traen un borrador que debes revisar y
> **reescribir con tus palabras** (la guía lo exige). Las secciones 3 a 5 dependen de **tus**
> mediciones: todo lo que está entre `[corchetes]` se reemplaza con tus datos. No dejes
> ninguna cifra que no hayas medido tú. Extensión final: 5 a 10 páginas.
> Para exportar a PDF: `pandoc Informe_Tecnico_Plantilla.md -o Informe.pdf` o pegarlo en Word/Google Docs.

---

## 1. Introducción

Casi toda aplicación empresarial depende de una base de datos, y la forma en que el código
accede a ella influye en el rendimiento, el tiempo de desarrollo, la seguridad y el costo de
mantenimiento durante toda la vida del sistema. En .NET existen dos enfoques principales:

- **ADO.NET**: el conjunto de clases base de .NET para conectarse a una base de datos,
  ejecutar SQL escrito por el programador y leer los resultados. El programador controla
  cada consulta y convierte manualmente las filas en objetos.
- **Entity Framework Core (EF Core)**: el ORM (mapeador objeto-relacional) oficial de
  Microsoft. Las tablas se representan con clases, las consultas se escriben en LINQ y EF
  genera el SQL. EF Core está construido **sobre** ADO.NET: internamente usa `DbConnection`,
  `DbCommand` y `DbDataReader`.

TechStore quiere decidir con datos reales cuál le conviene. Para ello se construyó el mismo
módulo dos veces, detrás de las mismas interfaces (`IProductoRepositorio` e
`IVentaRepositorio`), sobre el mismo esquema y con el mismo volumen de datos (50,000
productos), y se midieron siete escenarios de uso y el esfuerzo de desarrollo.

## 2. Desarrollo

### 2.1 Arquitectura de la solución

La aplicación de consola solo conoce las interfaces; la opción 1 del menú cambia la
implementación sin modificar el resto del código. Esto garantiza que ambas tecnologías
resuelven exactamente el mismo problema.

`[Insertar diagrama: Consola → interfaces → ADO.NET / EF Core → SQL Server]`

| Proyecto | Responsabilidad |
|---|---|
| TechStore.Core | Entidades, interfaces, `StockInsuficienteException` |
| TechStore.AdoNet | `ProductoRepositorioAdo`, `VentaRepositorioAdo` |
| TechStore.EFCore | `TechStoreContext` (Code First), migraciones, repositorios EF |
| TechStore.DatabaseFirst | Modelo generado con Scaffold-DbContext |
| TechStore.Consola | Menú, demostración de Rollback, pruebas de rendimiento |

### 2.2 Base de datos

Cinco tablas (Categoria, Producto, Cliente, Venta, DetalleVenta) con PK `IDENTITY`, FK,
`UNIQUE` en Codigo, Cedula, Email y Nombre de categoría, `CHECK` en Precio > 0, Stock >= 0 y
Cantidad > 0, y `DEFAULT GETDATE()` en Fecha.

**Carga de 50,000 productos.** En lugar de un `WHILE` con 50,000 `INSERT` individuales, se
generó una tabla de números con `ROW_NUMBER()` sobre un `CROSS JOIN` de `sys.all_objects` y
se insertaron todos los productos con **un solo `INSERT ... SELECT`** basado en conjuntos.
Los valores se calculan a partir del número de fila (sin `NEWID()` ni `RAND()`), lo que hace
la carga determinista: TechStoreDB y TechStoreDB_EF quedan idénticas. Tiempo de carga
medido: `[__ s]`.

### 2.3 ADO.NET: arquitectura e implementación

ADO.NET ofrece dos modos: el **conectado** (`SqlConnection`, `SqlCommand`, `SqlDataReader`),
que lee fila por fila con la conexión abierta, y el **desconectado** (`SqlDataAdapter`,
`DataSet`, `DataTable`), que trae los datos a memoria. Se usó el modo conectado por ser el
más eficiente para lectura.

Todas las consultas son parametrizadas. En `Buscar` el filtro se arma dinámicamente
concatenando solo fragmentos fijos del código; el texto del usuario viaja como parámetro:

```csharp
var sql = new StringBuilder(SelectBase).Append(" WHERE 1 = 1");
if (!string.IsNullOrWhiteSpace(texto))
{
    sql.Append(" AND Nombre LIKE @texto");
    parametros.Add(new SqlParameter("@texto", SqlDbType.NVarChar, 400)
        { Value = Utilidades.PatronContiene(texto) });
}
if (idCategoria.HasValue)
{
    sql.Append(" AND IdCategoria = @idCategoria");
    parametros.Add(new SqlParameter("@idCategoria", SqlDbType.Int) { Value = idCategoria.Value });
}
```

La venta usa `SqlTransaction`: inserta la cabecera, por cada producto lee precio y stock con
`UPDLOCK`, descuenta el stock con `UPDATE ... WHERE Stock >= @cantidad`, inserta el detalle y
al final actualiza el total. Cualquier excepción provoca `Rollback()`.

```csharp
using SqlTransaction tx = cn.BeginTransaction();
try
{
    // INSERT Venta → por cada ítem: SELECT (UPDLOCK) → UPDATE Stock → INSERT Detalle
    // → UPDATE Total
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```

`[Captura: demostración de Rollback (opción 11) con ADO.NET]`

### 2.4 Entity Framework: los tres enfoques

| Enfoque | Punto de partida | En este proyecto |
|---|---|---|
| **Code First** | Clases C# + `DbContext`; las migraciones crean la base | `TechStoreContext` + migración `InitialCreate` → TechStoreDB_EF |
| **Database First** | Base existente; `Scaffold-DbContext` genera las clases | `TechStore.DatabaseFirst`, generado desde TechStoreDB |
| **Model First** | Diagrama visual `.edmx`; la base se genera desde el modelo | No disponible en EF Core (ver 2.4.3) |

#### 2.4.1 Code First

Las relaciones y restricciones se configuraron con Fluent API para reproducir el esquema del
script SQL:

```csharp
modelBuilder.Entity<Producto>(e =>
{
    e.ToTable("Producto", t =>
    {
        t.HasCheckConstraint("CK_Producto_Precio", "[Precio] > 0");
        t.HasCheckConstraint("CK_Producto_Stock", "[Stock] >= 0");
    });
    e.Property(p => p.Precio).HasPrecision(10, 2);
    e.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UQ_Producto_Codigo");
    e.HasOne(p => p.Categoria).WithMany(c => c.Productos)
     .HasForeignKey(p => p.IdCategoria).OnDelete(DeleteBehavior.Restrict);
});
```

El reporte se resolvió con LINQ `GroupBy`:

```csharp
ctx.DetallesVenta.AsNoTracking()
   .Where(d => d.Venta!.Fecha >= desde && d.Venta.Fecha <= hasta)
   .GroupBy(d => d.Producto!.Categoria!.Nombre)
   .OrderByDescending(g => g.Sum(d => d.Cantidad * d.PrecioUnitario))
   .Select(g => new ReporteCategoria { Categoria = g.Key,
        UnidadesVendidas = g.Sum(d => d.Cantidad),
        TotalVendido = g.Sum(d => d.Cantidad * d.PrecioUnitario) });
```

SQL generado por EF (`ToQueryString()`, opción 13 de la consola):

```sql
[Pegar aquí la salida de la opción 13]
```

`[Comentario: compara este SQL con el escrito a mano en ADO.NET. ¿Usa los mismos JOIN? ¿Cómo nombra los alias? ¿Agrega algo?]`

`AjustarPrecios` tiene dos versiones: cargando las entidades (`ToList()` → modificar →
`SaveChanges()`, un `UPDATE` por fila) y con `ExecuteUpdate` (un único `UPDATE ... WHERE`).
La venta usa `Database.BeginTransaction()`; el descuento de stock se hace con
`ExecuteUpdate`, que participa en la transacción del contexto.

#### 2.4.2 Database First: código generado vs. Code First

| Aspecto | Code First (escrito a mano) | Database First (generado) |
|---|---|---|
| Navegación a la categoría | `producto.Categoria` | `producto.IdCategoriaNavigation` |
| Colección de productos | `categoria.Productos` | `categoria.Producto` |
| Clases | Compartidas con ADO.NET (Core) | `partial class` propias, `virtual` |
| Restricciones CHECK | Declaradas en Fluent API | `[¿Aparecen? revisa el código generado]` |
| `DeleteBehavior` | `Restrict` | `ClientSetNull` |
| Cambios futuros | Migración | Volver a ejecutar el scaffold (`--force`) |

`[Comentario propio: ¿qué modelo es más legible? ¿qué pasa si editas una clase generada y vuelves a hacer scaffold?]`

#### 2.4.3 Model First

`[Si NO hiciste el bonus:]` Model First consiste en dibujar el modelo en un diseñador visual
(archivo `.edmx`, basado en EDM/CSDL) y generar desde él la base de datos y las clases. Ese
flujo existe solo en **Entity Framework 6** con Visual Studio. EF Core abandonó el formato
EDMX: su modelo se construye exclusivamente en código (convenciones, atributos y Fluent API),
por lo que sus únicos puntos de partida son el código (Code First) o una base existente
(ingeniería inversa / Database First). Microsoft decidió no portar el diseñador por su
complejidad de mantenimiento, por los conflictos de fusión que generaba el XML del `.edmx`
en equipos que usan control de versiones y porque las migraciones de Code First cubren la
evolución del esquema.

## 3. Resultados

### 3.1 Equipo de prueba

```
[Pegar la salida de la opción 15 / el encabezado del archivo resultados/rendimiento_*.md]
Procesador: ...   RAM: ...   SO: ...   SQL Server: ...   .NET: ...   Compilación: Release
```

### 3.2 Metodología

Cada prueba se ejecutó 5 veces con `Stopwatch` en modo Release; se descartó la primera
(calentamiento: JIT, pool de conexiones, caché de planes de SQL Server y construcción del
modelo de EF) y se promediaron las otras 4. La preparación y limpieza de datos se hicieron
fuera del cronómetro. Ambas tecnologías trabajaron con los mismos datos y la misma secuencia
de Ids aleatorios (semilla fija).

### 3.3 Rendimiento

`[Pegar la tabla del archivo resultados/rendimiento_*.md]`

| # | Prueba | ADO.NET (ms) | EF Core (ms) | Variante EF | EF / ADO |
|---|---|---:|---:|---|---:|
| 1 | Leer 1,000 | [ ] | [ ] | AsNoTracking | [ ] |
| 1 | Leer 1,000 | | [ ] | Con tracking | [ ] |
| 1 | Leer 10,000 | [ ] | [ ] | AsNoTracking | [ ] |
| 1 | Leer 10,000 | | [ ] | Con tracking | [ ] |
| 1 | Leer 50,000 | [ ] | [ ] | AsNoTracking | [ ] |
| 1 | Leer 50,000 | | [ ] | Con tracking | [ ] |
| 2 | 1,000 búsquedas por Id | [ ] | [ ] | | [ ] |
| 3 | 100 búsquedas con filtros | [ ] | [ ] | | [ ] |
| 4 | Insertar 1,000 uno a uno | [ ] | [ ] | | [ ] |
| 4 | Insertar 1,000 en bloque | [ ] | [ ] | | [ ] |
| 5 | AjustarPrecios | [ ] | [ ] | Entidades | [ ] |
| 5 | AjustarPrecios | | [ ] | ExecuteUpdate | [ ] |
| 6 | Reporte por categoría | [ ] | [ ] | | [ ] |
| 7 | 100 ventas transaccionales | [ ] | [ ] | | [ ] |

![Relación EF/ADO](img/rendimiento_resumen.png)

`[Agrega los gráficos por escenario que necesites: img/rendimiento_escenario_N.png]`

### 3.4 Métricas de desarrollo

| Métrica | ADO.NET | EF Core |
|---|---:|---:|
| Líneas de código (repositorios) | [310] | [221] (+ [107] de configuración) |
| Tiempo de desarrollo | [ ] min | [ ] min |
| Errores en compilación | [ ] | [ ] |
| Errores en ejecución | [ ] | [ ] |
| Cambio "Marca": archivos / líneas escritas a mano | [2 / +17 −7] | [1 / +1] + migración generada |

## 4. Análisis

> Explica el **porqué** de cada diferencia con tus números. Preguntas guía:

**Rendimiento por escenario.**
- Lectura masiva: ¿cuánto cuesta la materialización de EF frente a `Mapear()` manual? ¿Cuánto
  agrega el tracking y por qué crece con el volumen (EF guarda una instantánea de cada
  entidad para detectar cambios)?
- Búsquedas por Id (muchas consultas pequeñas): aquí pesa el costo fijo por operación
  (crear el `DbContext`, traducir o reutilizar la consulta compilada). ¿Qué tan grande es?
- Actualización masiva: compara entidades + `SaveChanges` (N sentencias, aunque agrupadas
  en lotes) contra un solo `UPDATE`. ¿`ExecuteUpdate` queda cerca de ADO.NET?
- Inserción: uno a uno vs. en bloque. ¿Cuánto gana `SqlBulkCopy`? ¿Y el batching de EF?
- Reporte y ventas: ¿la diferencia es relevante cuando el trabajo lo hace SQL Server?

**Facilidad de uso.** LOC, tiempo y errores: ¿cuáles errores detectó el compilador en EF
que en ADO.NET solo aparecieron al ejecutar (nombres de columnas en cadenas, orden de
`GetInt32(5)`)?

**Control sobre consultas y transacciones.** En ADO.NET el SQL y los bloqueos (`UPDLOCK`)
son explícitos. En EF se revisó el SQL con `ToQueryString()`; ¿hubo que recurrir a
`ExecuteUpdate` o SQL crudo para lograr el mismo control?

**Escalabilidad y mantenimiento.** Usa el experimento de la columna `Marca`: archivos y
líneas tocadas, y qué tanto dependió del compilador para encontrar los lugares a cambiar.

## 5. Conclusiones y recomendación para TechStore

`[Escribe tu recomendación justificada con TUS resultados. Estructura sugerida:]`

| Parte del sistema | Tecnología recomendada | Justificación (dato de la sección 3) |
|---|---|---|
| CRUD de productos, clientes, pantallas de mantenimiento | [ ] | [ ] |
| Registro de ventas | [ ] | [ ] |
| Reportes y consultas pesadas | [ ] | [ ] |
| Cargas y actualizaciones masivas | [ ] | [ ] |

`[Cierre: ¿ADO.NET, EF Core o una combinación? Recuerda que EF Core permite usar SQL crudo
(FromSql, ExecuteSql) y que ambas tecnologías pueden convivir en la misma solución.]`

## 6. Referencias

Fowler, M. (2002). *Patterns of enterprise application architecture*. Addison-Wesley.

Lerman, J., & Miller, R. (2011). *Programming Entity Framework: Code First*. O'Reilly Media.

Microsoft. (s.f.-a). *ADO.NET overview*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/dotnet/framework/data/adonet/ado-net-overview

Microsoft. (s.f.-b). *Entity Framework Core*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/ef/core/

Microsoft. (s.f.-c). *Efficient querying*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/ef/core/performance/efficient-querying

Microsoft. (s.f.-d). *ExecuteUpdate and ExecuteDelete*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete

Microsoft. (s.f.-e). *Using transactions*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/ef/core/saving/transactions

Microsoft. (s.f.-f). *Migrations overview*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/ef/core/managing-schemas/migrations/

Microsoft. (s.f.-g). *Scaffolding (reverse engineering)*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/ef/core/managing-schemas/scaffolding/

Microsoft. (s.f.-h). *SqlBulkCopy class*. Microsoft Learn. Recuperado el [fecha] de https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlbulkcopy

Smith, J. P. (2021). *Entity Framework Core in action* (2.ª ed.). Manning.

`[Verifica cada enlace antes de entregar y agrega las fuentes que consultes tú.]`
