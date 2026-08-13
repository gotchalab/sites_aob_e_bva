"""
Extrai todas as coordenadas de todos os textos do template inscricao-socio-template.pdf.
Coordenadas em pontos PDF, origem TOP-LEFT (para bater com PDFsharp).
"""
import pdfplumber
import json
import sys

pdf_path = "d:/PROJETOS/aob/backend/src/AOB.Application/Templates/inscricao-socio-template.pdf"

with pdfplumber.open(pdf_path) as pdf:
    page = pdf.pages[0]
    print(f"Page size: width={page.width}pt height={page.height}pt")
    print(f"MediaBox: {page.mediabox}")
    print()

    # pdfplumber usa Y bottom-left (PDF native). Convertemos para top-left (PDFsharp compat).
    words = page.extract_words()
    print(f"Total words: {len(words)}")
    print()
    print(f"{'text':<40} {'x0':>7} {'y_top':>7} {'x1':>7} {'y_bot':>7}")
    print('-' * 80)

    # x0/top são já top-left origin em pdfplumber (top é distancia do topo)
    for w in words:
        text = w['text']
        x0 = w['x0']
        top = w['top']          # top-left Y from top of page
        x1 = w['x1']
        bottom = w['bottom']
        print(f"{text:<40} {x0:>7.1f} {top:>7.1f} {x1:>7.1f} {bottom:>7.1f}")

    # Extrair chars (útil para pontos individuais como "S. C. D.")
    print()
    print("---- CHARS (para labels S. C. D. V.) ----")
    chars = page.chars
    interesting = [c for c in chars if c['text'] in ['S', 'C', 'D', 'V', '.', ':', '☐']]
    for c in interesting[:80]:
        print(f"'{c['text']}' x={c['x0']:.1f} top={c['top']:.1f} bot={c['bottom']:.1f}")
