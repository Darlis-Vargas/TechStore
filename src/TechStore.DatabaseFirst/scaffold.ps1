# Database First: genera (o regenera) el modelo desde TechStoreDB.
# Ejecutar desde esta carpeta (src/TechStore.DatabaseFirst).
#   Requiere: dotnet tool install --global dotnet-ef --version 8.*
#
# Equivalente en la Consola del Administrador de Paquetes de Visual Studio:
#   Scaffold-DbContext "Server=localhost;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;" `
#       Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models -Context TechStoreDbFirstContext `
#       -NoOnConfiguring -NoPluralize -Force

param(
    [string]$Cadena = "Server=localhost;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;"
)

dotnet ef dbcontext scaffold $Cadena Microsoft.EntityFrameworkCore.SqlServer `
    --output-dir Models `
    --context TechStoreDbFirstContext `
    --no-onconfiguring `
    --no-pluralize `
    --force
