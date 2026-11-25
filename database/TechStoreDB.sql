/* =====================================================================
   TechStoreDB.sql
   Práctica TechStore: ADO.NET vs. Entity Framework Core
   ---------------------------------------------------------------------
   1. Crea la base de datos TechStoreDB (la borra si ya existe).
   2. Crea las 5 tablas con PK, FK, IDENTITY, UNIQUE, CHECK y DEFAULT.
   3. Carga datos de prueba: 5 categorías, 50,000 productos,
      100 clientes y 1,000 ventas (3 detalles por venta).

   Ejecución:
     - SSMS / Azure Data Studio: abrir el archivo y ejecutar (F5).
     - Línea de comandos:  sqlcmd -S localhost -E -i TechStoreDB.sql
   ===================================================================== */

USE master;
GO

IF DB_ID(N'TechStoreDB') IS NOT NULL
BEGIN
    ALTER DATABASE TechStoreDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TechStoreDB;
END
GO

CREATE DATABASE TechStoreDB;
GO

USE TechStoreDB;
GO

/* ---------------------------------------------------------------------
   Tablas
   Los nombres de las restricciones son explícitos para que:
     a) los mensajes de error sean legibles, y
     b) el modelo de Code First (TechStoreDB_EF) use los mismos nombres
        y Database First (Scaffold-DbContext) genere un modelo estable.
   --------------------------------------------------------------------- */

CREATE TABLE dbo.Categoria
(
    IdCategoria INT IDENTITY(1,1) NOT NULL,
    Nombre      NVARCHAR(50)      NOT NULL,
    CONSTRAINT PK_Categoria        PRIMARY KEY (IdCategoria),
    CONSTRAINT UQ_Categoria_Nombre UNIQUE (Nombre)
);

CREATE TABLE dbo.Producto
(
    IdProducto  INT IDENTITY(1,1) NOT NULL,
    Codigo      NVARCHAR(20)      NOT NULL,
    Nombre      NVARCHAR(100)     NOT NULL,
    Precio      DECIMAL(10,2)     NOT NULL,
    Stock       INT               NOT NULL,
    IdCategoria INT               NOT NULL,
    CONSTRAINT PK_Producto          PRIMARY KEY (IdProducto),
    CONSTRAINT UQ_Producto_Codigo   UNIQUE (Codigo),
    CONSTRAINT CK_Producto_Precio   CHECK (Precio > 0),
    CONSTRAINT CK_Producto_Stock    CHECK (Stock >= 0),
    CONSTRAINT FK_Producto_Categoria FOREIGN KEY (IdCategoria)
        REFERENCES dbo.Categoria (IdCategoria)
);

CREATE TABLE dbo.Cliente
(
    IdCliente INT IDENTITY(1,1) NOT NULL,
    Cedula    NVARCHAR(13)      NOT NULL,
    Nombre    NVARCHAR(100)     NOT NULL,
    Email     NVARCHAR(100)     NOT NULL,
    CONSTRAINT PK_Cliente        PRIMARY KEY (IdCliente),
    CONSTRAINT UQ_Cliente_Cedula UNIQUE (Cedula),
    CONSTRAINT UQ_Cliente_Email  UNIQUE (Email)
);

CREATE TABLE dbo.Venta
(
    IdVenta   INT IDENTITY(1,1) NOT NULL,
    IdCliente INT               NOT NULL,
    Fecha     DATETIME2         NOT NULL CONSTRAINT DF_Venta_Fecha DEFAULT (GETDATE()),
    Total     DECIMAL(12,2)     NOT NULL,
    CONSTRAINT PK_Venta         PRIMARY KEY (IdVenta),
    CONSTRAINT CK_Venta_Total   CHECK (Total >= 0),
    CONSTRAINT FK_Venta_Cliente FOREIGN KEY (IdCliente)
        REFERENCES dbo.Cliente (IdCliente)
);

CREATE TABLE dbo.DetalleVenta
(
    IdDetalle      INT IDENTITY(1,1) NOT NULL,
    IdVenta        INT               NOT NULL,
    IdProducto     INT               NOT NULL,
    Cantidad       INT               NOT NULL,
    PrecioUnitario DECIMAL(10,2)     NOT NULL,
    CONSTRAINT PK_DetalleVenta              PRIMARY KEY (IdDetalle),
    CONSTRAINT CK_DetalleVenta_Cantidad     CHECK (Cantidad > 0),
    CONSTRAINT CK_DetalleVenta_PrecioUnitario CHECK (PrecioUnitario > 0),
    CONSTRAINT FK_DetalleVenta_Venta    FOREIGN KEY (IdVenta)
        REFERENCES dbo.Venta (IdVenta),
    CONSTRAINT FK_DetalleVenta_Producto FOREIGN KEY (IdProducto)
        REFERENCES dbo.Producto (IdProducto)
);

/* Índices sobre las FK y la fecha (SQL Server no los crea solo). */
CREATE INDEX IX_Producto_IdCategoria    ON dbo.Producto (IdCategoria);
CREATE INDEX IX_Venta_IdCliente         ON dbo.Venta (IdCliente);
CREATE INDEX IX_Venta_Fecha             ON dbo.Venta (Fecha);
CREATE INDEX IX_DetalleVenta_IdVenta    ON dbo.DetalleVenta (IdVenta);
CREATE INDEX IX_DetalleVenta_IdProducto ON dbo.DetalleVenta (IdProducto);
GO

/* =====================================================================
   Carga de datos de prueba
   ---------------------------------------------------------------------
   Este bloque es un único lote (sin GO) para que la aplicación de
   consola también pueda ejecutarlo sobre TechStoreDB_EF, después de
   aplicar las migraciones de Code First. Así ambas bases quedan con
   exactamente los mismos datos.

 
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
GO

/* ---------------------------------------------------------------------
   Verificación rápida
   --------------------------------------------------------------------- */
SELECT 'Categoria' AS Tabla, COUNT(*) AS Filas FROM dbo.Categoria
UNION ALL SELECT 'Producto',     COUNT(*) FROM dbo.Producto
UNION ALL SELECT 'Cliente',      COUNT(*) FROM dbo.Cliente
UNION ALL SELECT 'Venta',        COUNT(*) FROM dbo.Venta
UNION ALL SELECT 'DetalleVenta', COUNT(*) FROM dbo.DetalleVenta;
GO

/* Pruebas de las restricciones (descomenta una a la vez; todas deben fallar):
INSERT INTO dbo.Producto (Codigo, Nombre, Precio, Stock, IdCategoria) VALUES (N'P000001', N'Duplicado', 10, 1, 1); -- UNIQUE
INSERT INTO dbo.Producto (Codigo, Nombre, Precio, Stock, IdCategoria) VALUES (N'X1', N'Precio cero', 0, 1, 1);     -- CHECK Precio > 0
UPDATE dbo.Producto SET Stock = -1 WHERE IdProducto = 1;                                                            -- CHECK Stock >= 0
INSERT INTO dbo.Producto (Codigo, Nombre, Precio, Stock, IdCategoria) VALUES (N'X2', N'Sin categoría', 10, 1, 999); -- FK
*/
