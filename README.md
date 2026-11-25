# TechStore · ADO.NET vs. Entity Framework Core

Sistema de inventario y ventas implementado **dos veces**: una con ADO.NET y otra con
Entity Framework Core, detrás de las mismas interfaces y sobre el mismo esquema de base de
datos. Incluye pruebas de rendimiento y métricas de desarrollo para comparar ambas tecnologías.

**Autor:** Darlis Bienvenido Vargas · 2024-2487

## Contenido del repositorio

```
TechStore.sln
├── database/
│   ├── TechStoreDB.sql          ← crea la base, sus restricciones y carga 50,000 productos
│   ├── CambioMarca.sql          ← agrega la columna Marca (cambio de esquema de la Parte F)
│   └── SeedData.sql             ← carga de datos que la consola usa para TechStoreDB_EF
├── src/
│   ├── TechStore.Core/          ← entidades e interfaces comunes
│   ├── TechStore.AdoNet/        ← repositorios con ADO.NET
│   ├── TechStore.EFCore/        ← DbContext Code First, migraciones y repositorios con EF Core
│   ├── TechStore.DatabaseFirst/ ← modelo generado con Scaffold-DbContext
│   └── TechStore.Consola/       ← menú, demostración de Rollback y pruebas de rendimiento
├── resultados/                  ← resultados de las pruebas de rendimiento (CSV y MD)
├── capturas/                    ← capturas de pantalla de la ejecución
├── docs/                        ← informe técnico y gráficos
└── tools/                       ← script de gráficos y conteo de líneas de código
```

## Requisitos

- **.NET 8 SDK** o superior
- **SQL Server** 2016 o superior (probado con SQL Server 2019 Express)
- **Visual Studio 2022/2026** o la línea de comandos `dotnet`
- Herramienta de EF Core (solo para migraciones y Database First):
  ```
  dotnet tool install --global dotnet-ef --version 8.*
  ```

## Cómo ejecutarlo

### 1. Crear la base de datos TechStoreDB (para ADO.NET)

En SQL Server Management Studio, abre y ejecuta, **en este orden**:

1. `database/TechStoreDB.sql`: crea las 5 tablas con sus restricciones y carga 5 categorías,
   50,000 productos, 100 clientes y 1,000 ventas.
2. `database/CambioMarca.sql`: agrega la columna `Marca` a `Producto`.

O desde la terminal:

```
sqlcmd -S .\SQLEXPRESS -E -i database\TechStoreDB.sql
sqlcmd -S .\SQLEXPRESS -E -i database\CambioMarca.sql
```

### 2. Configurar la cadena de conexión

Abre `src/TechStore.Consola/appsettings.json` y ajusta `Server` a tu instancia de SQL Server:

```json
"ConnectionStrings": {
  "TechStoreDB":    "Server=.\\SQLEXPRESS;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;",
  "TechStoreDB_EF": "Server=.\\SQLEXPRESS;Database=TechStoreDB_EF;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Otros valores comunes de `Server`: `localhost`, `(localdb)\\MSSQLLocalDB`.

### 3. Ejecutar la aplicación

En Visual Studio:

1. Abre `TechStore.sln`.
2. Cambia la configuración de **Debug** a **Release**.
3. Clic derecho sobre **TechStore.Consola** → **Establecer como proyecto de inicio**.
4. Presiona **Ctrl + F5** (Iniciar sin depurar).

O desde la terminal, en la carpeta del repositorio:

```
dotnet run -c Release --project src/TechStore.Consola
```

### 4. Crear la base TechStoreDB_EF (Code First)

En el menú de la consola elige la **opción 14**. Aplica las migraciones de EF Core
(`InitialCreate` y `AgregarMarca`) y carga los mismos datos que TechStoreDB.
Con la **opción 15** puedes comprobar que ambas bases tienen 50,000 productos.

## Opciones del menú

| Opción | Qué hace |
|---|---|
| 1 | Cambia entre ADO.NET y EF Core |
| 2 – 9 | Listar, buscar, ver, agregar, actualizar y eliminar productos; ajustar precios; reporte de ventas |
| 10 | Registrar una venta (transaccional) |
| 11 | Demostración de Rollback: venta con stock insuficiente que no guarda nada |
| 12 | Pruebas de rendimiento de los 7 escenarios; guarda los resultados en `resultados/` |
| 13 | Muestra el SQL que genera EF Core (`ToQueryString`) |
| 14 | Crea y llena TechStoreDB_EF |
| 15 | Muestra la información del equipo y el conteo de filas de ambas bases |

## Database First

El proyecto `TechStore.DatabaseFirst` contiene el modelo generado desde TechStoreDB.
Para regenerarlo y ejecutarlo:

```
cd src\TechStore.DatabaseFirst
dotnet ef dbcontext scaffold "Server=.\SQLEXPRESS;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --context TechStoreDbFirstContext --no-onconfiguring --no-pluralize --force
dotnet run -- "Server=.\SQLEXPRESS;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

## Pruebas de rendimiento

- Cada escenario se ejecuta 5 veces; la primera se descarta (calentamiento) y se promedian las otras 4.
- La preparación y limpieza de datos se hace fuera del cronómetro.
- Las mediciones deben hacerse en **Release** y sin depurador.

| # | Escenario |
|---|---|
| 1 | Leer 1,000, 10,000 y 50,000 productos (EF con y sin AsNoTracking) |
| 2 | 1,000 búsquedas por Id aleatorio |
| 3 | 100 búsquedas combinando texto y categoría |
| 4 | Insertar 1,000 productos uno a uno y en bloque |
| 5 | Ajustar precios de una categoría completa (EF: entidades vs. ExecuteUpdate) |
| 6 | Reporte de ventas por categoría |
| 7 | 100 ventas de 3 productos cada una |

Los resultados obtenidos están en `resultados/` y su análisis en el informe técnico (`docs/`).
