# Parte F · Métricas de desarrollo

> Completa esta hoja **mientras trabajas**. Los tiempos y errores tienen que ser los tuyos:
> el informe pierde puntos si los datos no son propios.

## 1. Líneas de código (sin comentarios ni líneas en blanco)

Obtenidas con `python tools/contar_lineas.py` (o en Visual Studio: *Analizar > Calcular
métricas de código*, columna "Líneas de código ejecutable").

| Implementación | Archivos | LOC |
|---|---|---:|
| ADO.NET | ProductoRepositorioAdo.cs + VentaRepositorioAdo.cs | 310 |
| EF Core – repositorios | ProductoRepositorioEf.cs + VentaRepositorioEf.cs | 221 |
| EF Core – configuración | TechStoreContext.cs + TechStoreContextFactory.cs | 107 |
| EF Core – migraciones | generadas por la herramienta (no se cuentan como escritas a mano) | — |

Nota: el repositorio de EF tiene **dos** versiones de AjustarPrecios y dos métodos para
`ToQueryString`, que ADO.NET no necesita. Sin ellos la diferencia es mayor.
Vuelve a ejecutar el script si modificas el código.

## 2. Tiempo de desarrollo

| Tarea | ADO.NET | EF Core |
|---|---|---|
| Repositorio de productos | ___ min | ___ min |
| Repositorio de ventas (transacción) | ___ min | ___ min |
| Configuración (contexto, migraciones / cadena de conexión) | ___ min | ___ min |
| Depuración | ___ min | ___ min |
| **Total** | ___ min | ___ min |

## 3. Errores encontrados

| # | Tecnología | Descripción | ¿Compilación o ejecución? | Cómo se resolvió |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

Ejemplos típicos para reconocerlos: en ADO.NET, `IndexOutOfRangeException` o
`InvalidCastException` por el orden de columnas en `Mapear()`, o un parámetro sin agregar
(errores **en ejecución**). En EF, un `Include` faltante, una consulta LINQ que no se puede
traducir o una migración pendiente.

## 4. Esfuerzo ante el cambio (columna Marca)

Ver `docs/cambio-marca/README.md`. Pega aquí la salida de `git diff main --stat`:

```
(pegar)
```
