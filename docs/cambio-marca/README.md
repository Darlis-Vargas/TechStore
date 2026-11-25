# Parte F · Cambio de esquema: agregar la columna `Marca` a `Producto`

El cambio ya está preparado como parches de Git para que puedas **aplicarlo, medirlo y
mostrarlo**. Hazlo en una rama aparte para que el diff quede como evidencia:

```bash
git checkout -b cambio-marca
git apply docs/cambio-marca/1_comun_entidad.patch   # Producto.cs (lo comparten ambas)
git apply docs/cambio-marca/2_adonet.patch          # ALTER TABLE + ProductoRepositorioAdo
git apply docs/cambio-marca/3_efcore.patch          # Fluent API en TechStoreContext

# ADO.NET: aplicar el ALTER a TechStoreDB
sqlcmd -S localhost -E -i database/CambioMarca.sql

# EF Core: la migración se GENERA, no se escribe
cd src/TechStore.EFCore
dotnet ef migrations add AgregarMarca
dotnet ef database update
cd ../..

# Database First: regenerar el modelo
cd src/TechStore.DatabaseFirst && pwsh ./scaffold.ps1 && cd ../..

git add -A && git commit -m "Parte F: agregar columna Marca a Producto"
git diff main --stat        # archivos y líneas cambiadas (evidencia para el informe)
```

## Lo que cambia en cada implementación (escrito a mano)

| | Archivos | Líneas (+/−) | Qué hubo que tocar |
|---|---:|---:|---|
| Común | 1 | +1 | `Producto.cs`: propiedad `Marca` |
| ADO.NET | 2 | +17 / −7 | `CambioMarca.sql` (ALTER TABLE) y `ProductoRepositorioAdo.cs`: 2 SELECT, INSERT, UPDATE, `Mapear()` (columna 6 con `IsDBNull`), parámetro `@marca`, columna del `SqlBulkCopy` |
| EF Core | 1 | +1 | `TechStoreContext.cs`: `HasMaxLength(50)`. El ALTER lo genera `migrations add` |
| Database First | 0 | 0 | Se regenera el modelo con el comando |

Lo que debes **verificar y anotar tú**: si todo compiló al primer intento, qué errores
aparecieron (y si fueron de compilación o de ejecución) y cuánto tardaste. Un dato
interesante para el informe: en ADO.NET, si olvidas agregar `Marca` al `SELECT` el código
**compila igual** y el error solo aparece al ejecutar (o, peor, la marca queda vacía sin
avisar); en EF el mapeo es automático.

Después del cambio, vuelve a correr las pruebas 1 y 4 para ver si la columna nueva afecta los tiempos.
