"""Lista os form fields (AcroForm) do template PDF."""
import pypdf

pdf_path = "d:/PROJETOS/aob/backend/src/AOB.Application/Templates/inscricao-socio-template.pdf"

reader = pypdf.PdfReader(pdf_path)
print(f"Has AcroForm: {reader.get_form_text_fields() is not None}")
fields = reader.get_fields() or {}
print(f"Number of fields: {len(fields)}")
for name, field in fields.items():
    ft = field.get('/FT', '')  # field type: /Tx text, /Btn button
    ft_str = str(ft)
    if ft_str == '/Btn':
        ff = int(field.get('/Ff', 0))
        # Bit 16 = radio, bit 15 = pushbutton
        is_radio = bool(ff & (1 << 15))
        is_push = bool(ff & (1 << 16))
        kind = 'radio' if is_radio else ('push' if is_push else 'checkbox')
    else:
        kind = ft_str
    v = field.get('/V', '')
    kids = field.get('/Kids')
    kid_count = len(kids) if kids else 0
    rect = field.get('/Rect', '')
    print(f"  {name!r:<40} type={kind:<10} value={v!r:<15} kids={kid_count} rect={rect}")
