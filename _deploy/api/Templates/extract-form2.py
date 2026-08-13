"""Detalhes dos kids dos radios do template (para saber que valor exportar)."""
import pypdf

pdf_path = "d:/PROJETOS/aob/backend/src/AOB.Application/Templates/inscricao-socio-template.pdf"
reader = pypdf.PdfReader(pdf_path)
fields = reader.get_fields() or {}

for name, field in fields.items():
    ft = str(field.get('/FT', ''))
    kids = field.get('/Kids')
    if not kids:
        continue
    print(f"\n=== {name!r} (type={ft}) ===")
    for i, kid in enumerate(kids):
        kid_obj = kid.get_object() if hasattr(kid, 'get_object') else kid
        ap_n = kid_obj.get('/AP', {})
        n_dict = ap_n.get('/N', {}) if ap_n else {}
        # Os "on" states são as keys de /AP/N (excluindo "Off")
        keys = list(n_dict.keys()) if n_dict else []
        rect = kid_obj.get('/Rect', '')
        print(f"  kid[{i}] rect={rect} AP/N keys={keys}")
