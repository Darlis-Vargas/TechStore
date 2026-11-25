"""
Genera los gráficos del informe a partir del CSV más reciente de resultados/.

Uso (desde la raíz del repositorio):
    pip install matplotlib
    python tools/graficos.py                      # usa el CSV más reciente
    python tools/graficos.py resultados/rendimiento_20261201_101500.csv

Salida: docs/img/rendimiento_escenario_N.png (uno por escenario) y
        docs/img/rendimiento_resumen.png (relación EF/ADO).
Cada escenario va en su propio gráfico porque las escalas son muy distintas
(de milisegundos a segundos); mezclarlos en un solo eje ocultaría los pequeños.
"""
import csv
import glob
import os
import sys
from collections import OrderedDict

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SALIDA = os.path.join(RAIZ, "docs", "img")

# Colores fijos por serie (siempre el mismo color para la misma tecnología/variante)
AZUL, NARANJA, AQUA = "#257de0", "#f47230", "#00b894"
TEXTO, TEXTO_2, GRILLA = "#191919", "#666666", "#e6e6e6"


def color_serie(tecnologia, indice_ef):
    if tecnologia == "ADO.NET":
        return AZUL
    return NARANJA if indice_ef == 0 else AQUA


def leer(ruta):
    with open(ruta, encoding="utf-8") as f:
        return [
            {**r, "Escenario": int(r["Escenario"]), "PromedioMs": float(r["PromedioMs"]),
             "MinMs": float(r["MinMs"]), "MaxMs": float(r["MaxMs"])}
            for r in csv.DictReader(f)
        ]


def estilo(ax):
    for lado in ("top", "right", "left"):
        ax.spines[lado].set_visible(False)
    ax.spines["bottom"].set_color(GRILLA)
    ax.tick_params(colors=TEXTO_2, length=0)
    ax.xaxis.grid(True, color=GRILLA, linewidth=0.8)
    ax.set_axisbelow(True)


def grafico_escenario(num, filas):
    pruebas = list(OrderedDict.fromkeys(r["Prueba"] for r in filas))
    alto_fila = 0.38
    fig, axes = plt.subplots(len(pruebas), 1, figsize=(8, 0.9 + len(filas) * alto_fila + 0.5 * len(pruebas)),
                             squeeze=False)
    for ax, prueba in zip(axes[:, 0], pruebas):
        grupo = [r for r in filas if r["Prueba"] == prueba]
        etiquetas, valores, colores = [], [], []
        i_ef = 0
        for r in grupo:
            etiquetas.append(f'{r["Tecnologia"]} · {r["Variante"]}' if r["Variante"] else r["Tecnologia"])
            valores.append(r["PromedioMs"])
            colores.append(color_serie(r["Tecnologia"], i_ef))
            if r["Tecnologia"] != "ADO.NET":
                i_ef += 1
        y = list(range(len(grupo)))[::-1]
        ax.barh(y, valores, color=colores, height=0.62, edgecolor="white", linewidth=2)
        ax.set_yticks(y, etiquetas, color=TEXTO, fontsize=9)
        maximo = max(valores) if valores else 1
        for yy, v in zip(y, valores):
            ax.text(v + maximo * 0.01, yy, f"{v:,.1f} ms", va="center", fontsize=9, color=TEXTO)
        ax.set_xlim(0, maximo * 1.18)
        ax.set_title(prueba, loc="left", fontsize=10, color=TEXTO)
        estilo(ax)
    axes[-1, 0].set_xlabel("Tiempo promedio (ms) · menos es mejor", color=TEXTO_2, fontsize=9)
    fig.suptitle(f"Escenario {num}", x=0.02, ha="left", fontsize=12, color=TEXTO, fontweight="bold")
    fig.tight_layout()
    ruta = os.path.join(SALIDA, f"rendimiento_escenario_{num}.png")
    fig.savefig(ruta, dpi=160)
    plt.close(fig)
    return ruta


def grafico_resumen(filas):
    etiquetas, ratios, colores = [], [], []
    claves = list(OrderedDict.fromkeys((r["Escenario"], r["Prueba"]) for r in filas))
    for esc, prueba in claves:
        grupo = [r for r in filas if r["Escenario"] == esc and r["Prueba"] == prueba]
        ado = next((r for r in grupo if r["Tecnologia"] == "ADO.NET"), None)
        if not ado or ado["PromedioMs"] <= 0:
            continue
        for i, ef in enumerate(r for r in grupo if r["Tecnologia"] == "EF Core"):
            etiquetas.append(f"{esc}. {prueba} — {ef['Variante']}")
            ratios.append(ef["PromedioMs"] / ado["PromedioMs"])
            colores.append(color_serie("EF Core", i))
    if not ratios:
        return None
    fig, ax = plt.subplots(figsize=(9, 0.8 + 0.34 * len(ratios)))
    y = list(range(len(ratios)))[::-1]
    ax.barh(y, ratios, color=colores, height=0.62, edgecolor="white", linewidth=2)
    ax.axvline(1.0, color=TEXTO_2, linewidth=1, linestyle="--")
    ax.text(1.0, max(y) + 0.7, "ADO.NET = 1.0", ha="center", fontsize=8, color=TEXTO_2)
    ax.set_yticks(y, etiquetas, fontsize=8, color=TEXTO)
    for yy, v in zip(y, ratios):
        ax.text(v + max(ratios) * 0.01, yy, f"x{v:.2f}", va="center", fontsize=8, color=TEXTO)
    ax.set_xlim(0, max(ratios) * 1.15)
    ax.set_xlabel("Tiempo de EF Core ÷ tiempo de ADO.NET  (>1 = EF más lento)", color=TEXTO_2, fontsize=9)
    ax.set_title("Relación EF Core / ADO.NET por prueba", loc="left", fontsize=12, color=TEXTO, fontweight="bold")
    estilo(ax)
    fig.tight_layout()
    ruta = os.path.join(SALIDA, "rendimiento_resumen.png")
    fig.savefig(ruta, dpi=160)
    plt.close(fig)
    return ruta


def main():
    if len(sys.argv) > 1:
        ruta = sys.argv[1]
    else:
        archivos = sorted(glob.glob(os.path.join(RAIZ, "resultados", "rendimiento_*.csv")))
        if not archivos:
            sys.exit("No hay CSV en resultados/. Ejecuta primero la opción 12 de la consola.")
        ruta = archivos[-1]
    print(f"Leyendo {ruta}")
    filas = leer(ruta)
    os.makedirs(SALIDA, exist_ok=True)
    for num in sorted({r["Escenario"] for r in filas}):
        print("  ", grafico_escenario(num, [r for r in filas if r["Escenario"] == num]))
    resumen = grafico_resumen(filas)
    if resumen:
        print("  ", resumen)


if __name__ == "__main__":
    main()
