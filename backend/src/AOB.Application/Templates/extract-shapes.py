"""Extrai formas (círculos, checkboxes, linhas) do template."""
import pdfplumber

pdf_path = "d:/PROJETOS/aob/backend/src/AOB.Application/Templates/inscricao-socio-template.pdf"

with pdfplumber.open(pdf_path) as pdf:
    page = pdf.pages[0]
    # Curves (círculos)
    curves = page.curves
    print(f"---- CURVES ({len(curves)}) ----")
    for c in curves:
        # Cada curve tem bbox
        x0, top, x1, bot = c['x0'], c['top'], c['x1'], c['bottom']
        w, h = x1 - x0, bot - top
        cx, cy = (x0 + x1) / 2, (top + bot) / 2
        # Filter circles-like (small square-ish bbox)
        if 8 < w < 25 and 8 < h < 25 and abs(w - h) < 6:
            print(f"  circle-like: bbox=({x0:.1f}, {top:.1f})-({x1:.1f}, {bot:.1f}) center=({cx:.1f}, {cy:.1f}) size={w:.1f}x{h:.1f}")

    # Rects (checkboxes quadrados)
    rects = page.rects
    print(f"\n---- RECTS ({len(rects)}) ----")
    for r in rects:
        x0, top, x1, bot = r['x0'], r['top'], r['x1'], r['bottom']
        w, h = x1 - x0, bot - top
        cx, cy = (x0 + x1) / 2, (top + bot) / 2
        # Small square-ish = checkboxes
        if 8 < w < 25 and 8 < h < 25 and abs(w - h) < 6:
            print(f"  checkbox: bbox=({x0:.1f}, {top:.1f})-({x1:.1f}, {bot:.1f}) center=({cx:.1f}, {cy:.1f}) size={w:.1f}x{h:.1f}")
        # Big rects (blocos)
        elif w > 100 and h > 30:
            print(f"  block:    bbox=({x0:.1f}, {top:.1f})-({x1:.1f}, {bot:.1f}) size={w:.0f}x{h:.0f}")
