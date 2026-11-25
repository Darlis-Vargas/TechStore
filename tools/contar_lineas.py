"""
Cuenta líneas de código (sin líneas en blanco ni comentarios) de cada
repositorio, para la Parte F. Uso:  python tools/contar_lineas.py
"""
import os
import re

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARCHIVOS = {
    "ADO.NET": ["src/TechStore.AdoNet/ProductoRepositorioAdo.cs",
                "src/TechStore.AdoNet/VentaRepositorioAdo.cs"],
    "EF Core (repositorios)": ["src/TechStore.EFCore/ProductoRepositorioEf.cs",
                               "src/TechStore.EFCore/VentaRepositorioEf.cs"],
    "EF Core (DbContext + fábrica)": ["src/TechStore.EFCore/TechStoreContext.cs",
                                      "src/TechStore.EFCore/TechStoreContextFactory.cs"],
}


def lineas_de_codigo(ruta):
    texto = open(os.path.join(RAIZ, ruta), encoding="utf-8").read()
    texto = re.sub(r"/\*.*?\*/", "", texto, flags=re.S)          # comentarios de bloque
    total = 0
    for linea in texto.splitlines():
        s = linea.strip()
        if not s or s.startswith("//"):
            continue
        total += 1
    return total


if __name__ == "__main__":
    print(f"{'Implementación':<32}{'Archivo':<48}{'LOC':>6}")
    for grupo, rutas in ARCHIVOS.items():
        suma = 0
        for r in rutas:
            n = lineas_de_codigo(r)
            suma += n
            print(f"{grupo:<32}{os.path.basename(r):<48}{n:>6}")
        print(f"{'':<32}{'TOTAL ' + grupo:<48}{suma:>6}\n")
