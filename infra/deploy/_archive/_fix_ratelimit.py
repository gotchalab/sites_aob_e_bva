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

# 1. Corrigir aob-zones.conf ($ não interpolado — Python string literal)
zones_conf = (
    "# Rate limit zones\n"
    "limit_req_zone $binary_remote_addr zone=aob_public:10m rate=120r/m;\n"
    "limit_req_zone $binary_remote_addr zone=aob_forms:10m  rate=10r/m;\n"
)

sftp = ssh.open_sftp()
sftp.putfo(io.BytesIO(zones_conf.encode()), "/tmp/aob-zones.conf")
sftp.close()
run("sudo cp /tmp/aob-zones.conf /etc/nginx/conf.d/aob-zones.conf")
run("sudo rm /tmp/aob-zones.conf")

# Verificar
out, _ = run("cat /etc/nginx/conf.d/aob-zones.conf")
print("aob-zones.conf:")
print(out)

# 2. Adicionar location /_next/static/ sem rate limit nos dois sites
def patch_site(path, next_port, site_name):
    out, _ = run(f"cat {path}")

    # Aumentar burst de 20 para 60
    out = out.replace(
        "limit_req zone=aob_public burst=20 nodelay;",
        "limit_req zone=aob_public burst=60 nodelay;"
    )

    # Adicionar location /_next/static/ antes de "location /" (apenas se não existir ainda)
    if "/_next/static/" not in out:
        static_block = (
            f"    location /_next/static/ {{\n"
            f"        proxy_pass http://127.0.0.1:{next_port};\n"
            f"        proxy_set_header Host $host;\n"
            f"        add_header Cache-Control \"public, max-age=31536000, immutable\";\n"
            f"    }}\n\n"
            f"    location / {{"
        )
        out = out.replace("    location / {", static_block, 1)

    sftp = ssh.open_sftp()
    sftp.putfo(io.BytesIO(out.encode()), "/tmp/site.conf")
    sftp.close()
    run(f"sudo cp /tmp/site.conf {path}")
    run("sudo rm /tmp/site.conf")
    print(f"  {site_name}: actualizado")

print("\nA actualizar sites nginx...")
patch_site("/etc/nginx/sites-enabled/aobarcelos.pt.conf",   3000, "aobarcelos.pt")
patch_site("/etc/nginx/sites-enabled/bva-p.aobarcelos.pt.conf", 3001, "bva-p.aobarcelos.pt")

# 3. Testar e recarregar nginx
print()
out, err = run("sudo nginx -t 2>&1")
print("nginx -t:", out or err)

if "ok" in (out + err).lower():
    run("sudo systemctl reload nginx")
    print("nginx recarregado OK")
else:
    print("ERRO — nginx não recarregou")
    # Mostrar o erro em detalhe
    out2, _ = run("sudo nginx -T 2>&1 | grep -A3 'emerg\\|error' | head -20")
    print(out2)

# 4. Confirmar rate limit
out, _ = run("sudo nginx -T 2>/dev/null | grep limit_req_zone")
print("\nlimit_req_zone activo:", out)

# 5. Confirmar fail2ban
out, _ = run("sudo fail2ban-client status 2>/dev/null")
print("\nfail2ban:", out)

# 6. Teste rápido
import time
time.sleep(1)
print("\nTeste final:")
for url in ["https://aobarcelos.pt/", "https://bva-p.aobarcelos.pt/"]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 '{url}'")
    print(f"  {url} -> {out}")

ssh.close()
