import sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=60):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# 1. Verificar DNS
print("=== DNS RESOLUTION ===")
for domain in ["admin.aobarcelos.pt", "api.aobarcelos.pt"]:
    out, err = run(f"dig +short {domain} A 2>/dev/null || host {domain} 2>/dev/null | head -2")
    print(f"  {domain}: {out or err or '(sem resposta)'}")

# 2. Verificar que nginx está a responder em HTTP para esses domínios
print("\n=== TESTE HTTP (antes do certbot) ===")
for domain in ["admin.aobarcelos.pt", "api.aobarcelos.pt"]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 8 http://{domain}/ 2>&1")
    print(f"  http://{domain}/ -> {out}")

# 3. Correr certbot
print("\n=== CERTBOT ===")
out, err = run(
    "sudo certbot --nginx --non-interactive --agree-tos -m bfilipemv@gmail.com "
    "-d admin.aobarcelos.pt -d api.aobarcelos.pt 2>&1",
    timeout=120
)
print(out or err)

# 4. Recarregar nginx
print("\n=== RELOAD NGINX ===")
out, err = run("sudo nginx -t 2>&1 && sudo systemctl reload nginx")
print(out or err or "OK")

# 5. Testar HTTPS
print("\n=== TESTES HTTPS FINAIS ===")
time.sleep(2)
for domain, path in [
    ("admin.aobarcelos.pt", "/"),
    ("api.aobarcelos.pt", "/api/sites/aob"),
    ("api.aobarcelos.pt", "/api/sites/bva"),
]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 https://{domain}{path} 2>&1")
    print(f"  https://{domain}{path} -> {out}")

# 6. Listar todos os certs
print("\n=== CERTIFICADOS ACTIVOS ===")
out, _ = run("sudo certbot certificates 2>/dev/null | grep -E 'Domains:|Expiry'")
print(out)

ssh.close()
