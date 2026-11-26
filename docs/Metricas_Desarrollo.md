# Parte F · Métricas de desarrollo

## 1. Líneas de código (sin comentarios ni líneas en blanco)

Obtenidas con `python tools/contar_lineas.py`.

| Implementación | Archivos | LOC |
|---|---|---:|
| ADO.NET | ProductoRepositorioAdo.cs + VentaRepositorioAdo.cs | 310 |
| EF Core – repositorios | ProductoRepositorioEf.cs + VentaRepositorioEf.cs | 221 |
| EF Core – configuración | TechStoreContext.cs + TechStoreContextFactory.cs | 107 |
| EF Core – migraciones | Generadas por la herramienta (no se cuentan) | — |

## 2. Tiempo de configuración, ejecución y verificación

Calculado con las marcas de tiempo de cada resultado confirmado.

| Tarea | ADO.NET | EF Core |
|---|---:|---:|
| Crear la base de datos | 39 min (TechStoreDB y prueba de restricciones) | 25 min (TechStoreDB_EF con migraciones) |
| Demostración de Rollback | 6 min | 7 min |
| SQL generado (`ToQueryString`) | — | 10 min |
| Database First | — | 20 min |
| Cambio de la columna Marca | 32 min | 12 min |
| **Total** | **77 min** | **74 min** |

Tiempo común a ambas, no incluido: compilación y configuración (20 min) y pruebas de rendimiento (9 min).

## 3. Errores encontrados

| # | Tecnología | Descripción | Tipo | Solución |
|---|---|---|---|---|
| 1 | ADO.NET | La cadena de conexión apuntaba a `localhost` y mi instancia es `.\SQLEXPRESS` | Configuración | Cambié `Server` en `appsettings.json` |
| 2 | EF Core | El scaffold falló con "No project was found" | Ejecución | Ejecuté el comando desde la carpeta del proyecto DatabaseFirst |

Errores de compilación: 0 en ambas tecnologías.

## 4. Esfuerzo ante el cambio (columna Marca)

| | ADO.NET | EF Core |
|---|---|---|
| Archivos escritos a mano | 2 (`CambioMarca.sql` y `ProductoRepositorioAdo.cs`) | 1 (`TechStoreContext.cs`) |
| Líneas escritas a mano | +17 / −7 | +1 |
| Cambio en la base de datos | `ALTER TABLE` escrito a mano | Migración `AgregarMarca` generada por EF |

Común a ambas: 1 línea en `Producto.cs` (la propiedad `Marca`).
