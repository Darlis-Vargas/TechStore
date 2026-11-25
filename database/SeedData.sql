/* =====================================================================
   Carga de datos de prueba
   ---------------------------------------------------------------------
   Este bloque es un único lote (sin GO) para que la aplicación de
   consola también pueda ejecutarlo sobre TechStoreDB_EF, después de
   aplicar las migraciones de Code First. Así ambas bases quedan con
   exactamente los mismos datos.

   ¿Cómo se generan 50,000 filas rápido?
   Un WHILE con 50,000 INSERT individuales hace 50,000 operaciones
   (y 50,000 escrituras al log si no hay transacción). En su lugar se
   crea una "tabla de números" (#Numeros) con un CROSS JOIN de
   sys.all_objects consigo misma (miles x miles de filas) y ROW_NUMBER().
   Luego un solo INSERT ... SELECT genera todos los productos en bloque,
   en una sola operación basada en conjuntos. Tarda alrededor de un
   segundo en lugar de varios minutos.

   Los datos son DETERMINISTAS (no usan NEWID() ni RAND()): todos los
   valores se calculan a partir del número de fila. Por eso TechStoreDB y
   TechStoreDB_EF tienen exactamente los mismos productos, precios,
   stocks y ventas, y la comparación de rendimiento es justa.
   ===================================================================== */
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.Producto)
BEGIN
    PRINT N'La base de datos ya tiene productos. No se cargan datos de nuevo.';
    RETURN;
END

/* 1. Tabla de números 1..50,000 */
IF OBJECT_ID(N'tempdb..#Numeros') IS NOT NULL DROP TABLE #Numeros;

SELECT TOP (50000)
       CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT) AS n
INTO   #Numeros
FROM   sys.all_objects AS a
CROSS JOIN sys.all_objects AS b;

/* 2. Categorías (5) */
INSERT INTO dbo.Categoria (Nombre)
VALUES (N'Laptops'), (N'Smartphones'), (N'Accesorios'), (N'Monitores'), (N'Componentes');

/* 3. Productos (50,000) en un solo INSERT ... SELECT
      El número de categoría (1..5) se asocia a su IdCategoria real con
      ROW_NUMBER(), para no depender de que el IDENTITY empiece en 1. */
WITH Cat AS
(
    SELECT IdCategoria, ROW_NUMBER() OVER (ORDER BY IdCategoria) AS rn
    FROM dbo.Categoria
)
INSERT INTO dbo.Producto (Codigo, Nombre, Precio, Stock, IdCategoria)
SELECT
    CONCAT(N'P', RIGHT(CONCAT(N'000000', x.n), 6)),
    CONCAT(
        CHOOSE(x.cat,
            CHOOSE(x.n % 3 + 1, N'Laptop', N'Notebook', N'Ultrabook'),
            CHOOSE(x.n % 3 + 1, N'Smartphone', N'Teléfono', N'Phablet'),
            CHOOSE(x.n % 4 + 1, N'Mouse', N'Teclado', N'Audífonos', N'Cargador'),
            CHOOSE(x.n % 3 + 1, N'Monitor', N'Pantalla', N'Display'),
            CHOOSE(x.n % 4 + 1, N'SSD', N'Memoria RAM', N'Tarjeta Gráfica', N'Procesador')),
        N' ',
        CHOOSE(x.n % 6 + 1, N'Dell', N'HP', N'Lenovo', N'Samsung', N'Asus', N'Acer'),
        N' ',
        CHOOSE((x.n / 7) % 4 + 1, N'Pro', N'Air', N'Max', N'Lite'),
        N' ',
        x.n),
    CAST(9.99 + (x.n * 7919 % 290000) / 100.0 AS DECIMAL(10,2)),   -- 9.99 .. 2,909.98
    50 + (x.n * 31 % 951),                                          -- 50 .. 1,000 unidades
    c.IdCategoria
FROM (SELECT n, ((n - 1) % 5) + 1 AS cat FROM #Numeros) AS x
JOIN Cat AS c ON c.rn = x.cat
ORDER BY x.n;

/* 4. Clientes (100) */
INSERT INTO dbo.Cliente (Cedula, Nombre, Email)
SELECT
    CONCAT(N'001-', RIGHT(CONCAT(N'0000000', n), 7), N'-', n % 10),
    CONCAT(
        CHOOSE(n % 10 + 1, N'Ana', N'Luis', N'María', N'José', N'Carmen',
                           N'Pedro', N'Laura', N'Miguel', N'Rosa', N'Carlos'),
        N' ',
        CHOOSE((n / 10) % 10 + 1, N'Pérez', N'Gómez', N'Rodríguez', N'Martínez', N'Santos',
                                  N'Reyes', N'Díaz', N'Castillo', N'Núñez', N'Jiménez')),
    CONCAT(N'cliente', n, N'@techstore.com')
FROM #Numeros
WHERE n <= 100
ORDER BY n;

/* 5. Ventas (1,000) con 3 detalles cada una */
DECLARE @minCliente  INT = (SELECT MIN(IdCliente)  FROM dbo.Cliente);
DECLARE @minProducto INT = (SELECT MIN(IdProducto) FROM dbo.Producto);
DECLARE @minVenta    INT;

-- Fechas fijas (no GETDATE) para que ambas bases sean idénticas:
-- una venta cada 8 horas hacia atrás desde el 1 de noviembre de 2026.
INSERT INTO dbo.Venta (IdCliente, Fecha, Total)
SELECT @minCliente + ((n - 1) % 100),
       DATEADD(HOUR, -8 * n, CAST('2026-11-01T10:00:00' AS DATETIME2)),
       0
FROM #Numeros
WHERE n <= 1000
ORDER BY n;

SET @minVenta = (SELECT MIN(IdVenta) FROM dbo.Venta);

INSERT INTO dbo.DetalleVenta (IdVenta, IdProducto, Cantidad, PrecioUnitario)
SELECT @minVenta + v.n - 1,
       p.IdProducto,
       1 + (v.n + k.k) % 3,
       p.Precio
FROM #Numeros AS v
CROSS JOIN (VALUES (0), (1), (2)) AS k (k)
JOIN dbo.Producto AS p
  ON p.IdProducto = @minProducto + ((v.n * 37 + k.k * 15511) % 50000)
WHERE v.n <= 1000;

-- Total de cada venta = suma de sus detalles
UPDATE v
SET    v.Total = t.Total
FROM   dbo.Venta AS v
JOIN  (SELECT IdVenta, SUM(Cantidad * PrecioUnitario) AS Total
       FROM dbo.DetalleVenta
       GROUP BY IdVenta) AS t
  ON t.IdVenta = v.IdVenta;

DROP TABLE #Numeros;

PRINT N'Carga completada: 5 categorías, 50,000 productos, 100 clientes, 1,000 ventas y 3,000 detalles.';
