import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=20):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Ler o conf actual do aobarcelos
out_aob, _ = run("cat /etc/nginx/sites-enabled/aobarcelos.pt.conf")
out_bva, _ = run("cat /etc/nginx/sites-enabled/bva-p.aobarcelos.pt.conf")

# Remover o bloco /_next/static/ com erros e reconstruir correctamente
# Usar substituição de string Python pura (sem f-strings para o conteúdo nginx)
DOLLAR = "$"

def make_static_block(port):
    return (
        "    location /_next/static/ {\n"
        "        proxy_pass http://127.0.0.1:" + str(port) + ";\n"
        "        proxy_set_header Host " + DOLLAR + "host;\n"
        '        add_header Cache-Control "public, max-age=31536000, immutable";\n'
        "    }\n"
        "\n"
        "    # Frontend Next.js\n"
        "    location / {"
    )

# Remover bloco existente (pode ter erros) e reinserir correctamente
def clean_and_patch(content, port, old_marker):
    # Remover bloco /_next/static/ existente se tiver erros
    import re
    # Apagar qualquer bloco location /_next/static/ {...} já existente
    content = re.sub(
        r"    location /_next/static/ \{[^}]*\}\n\n",
        "",
        content,
        flags=re.DOTALL
    )
    # Substituir o marcador "# Frontend Next.js\n    location / {" pelo novo bloco
    if "/_next/static/" not in content:
        content = content.replace(old_marker, make_static_block(port), 1)
    return content

# aobarcelos.pt — porta 3000
marker_aob = "    # Frontend Next.js\n    location / {"
out_aob = clean_and_patch(out_aob, 3000, marker_aob)

# bva-p.aobarcelos.pt — porta 3001
marker_bva = "    location / {"
# Para o BVA, o primeiro "location / {" é o frontend
def clean_and_patch_bva(content, port):
    import re
    content = re.sub(
        r"    location /_next/static/ \{[^}]*\}\n\n",
        "",
        content,
        flags=re.DOTALL
    )
    if "/_next/static/" not in content:
        # Encontrar o location / { que é o frontend (o que tem limit_req)
        static_bl = (
            "    location /_next/static/ {\n"
            "        proxy_pass http://127.0.0.1:" + str(port) + ";\n"
            "        proxy_set_header Host " + DOLLAR + "host;\n"
            '        add_header Cache-Control "public, max-age=31536000, immutable";\n'
            "    }\n"
            "\n"
            "    location / {"
        )
        # Substitui o location / que tem limit_req (o frontend)
        content = content.replace("    location / {\n        limit_req", static_bl + "\n        limit_req", 1)
    return content

out_bva = clean_and_patch_bva(out_bva, 3001)

# Upload
sftp = ssh.open_sftp()
sftp.putfo(io.BytesIO(out_aob.encode()), "/tmp/aob.conf")
sftp.putfo(io.BytesIO(out_bva.encode()), "/tmp/bva.conf")
sftp.close()

run("sudo cp /tmp/aob.conf /etc/nginx/sites-enabled/aobarcelos.pt.conf && sudo rm /tmp/aob.conf")
run("sudo cp /tmp/bva.conf /etc/nginx/sites-enabled/bva-p.aobarcelos.pt.conf && sudo rm /tmp/bva.conf")

# Verificar a linha que estava errada
print("=== aobarcelos /_next/static/ block ===")
out, _ = run("grep -A5 '_next/static' /etc/nginx/sites-enabled/aobarcelos.pt.conf")
print(out)

print()
print("=== bva /_next/static/ block ===")
out, _ = run("grep -A5 '_next/static' /etc/nginx/sites-enabled/bva-p.aobarcelos.pt.conf")
print(out)

# Testar nginx
print()
out, err = run("sudo nginx -t 2>&1")
print("nginx -t:", out or err)

if "ok" in (out + err).lower():
    run("sudo systemctl reload nginx")
    print("nginx recarregado OK")
    import time; time.sleep(1)
    for url in ["https://aobarcelos.pt/", "https://bva-p.aobarcelos.pt/"]:
        r, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 '" + url + "'")
        print("  " + url + " -> " + r)

ssh.close()
