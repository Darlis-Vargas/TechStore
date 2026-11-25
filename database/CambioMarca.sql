/* Parte F: cambio de esquema para la implementación ADO.NET (TechStoreDB).
   En EF Core este cambio lo genera la migración AgregarMarca. */
USE TechStoreDB;
GO
IF COL_LENGTH('dbo.Producto', 'Marca') IS NULL
    ALTER TABLE dbo.Producto ADD Marca NVARCHAR(50) NULL;
GO
