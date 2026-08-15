import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko, time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=120):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Instalar certbot se necessario
out, _ = run("which certbot 2>&1")
if not out or "not found" in out:
    print("Instalando certbot...")
    run("sudo apt-get update -q && sudo apt-get install -y certbot python3-certbot-nginx 2>&1 | tail -5", timeout=120)
else:
    print(f"certbot ja instalado: {out}")

# Correr certbot para aobarcelos.pt e www
print("\nCertbot aobarcelos.pt + www...")
out, err = run(
    "sudo certbot --nginx --non-interactive --agree-tos "
    "-m bfilipemv@gmail.com "
    "-d aobarcelos.pt -d www.aobarcelos.pt "
    "2>&1",
    timeout=120
)
print(f"aobarcelos.pt cert:\n{out[-2000:]}\n")
if err and "error" in err.lower():
    print(f"[ERR] {err[:500]}")

# Correr certbot para bva-p.aobarcelos.pt
print("\nCertbot bva-p.aobarcelos.pt...")
out, err = run(
    "sudo certbot --nginx --non-interactive --agree-tos "
    "-m bfilipemv@gmail.com "
    "-d bva-p.aobarcelos.pt "
    "2>&1",
    timeout=120
)
print(f"bva-p cert:\n{out[-1500:]}\n")

# Correr certbot para bva-p-socios.aobarcelos.pt
print("\nCertbot bva-p-socios.aobarcelos.pt...")
out, err = run(
    "sudo certbot --nginx --non-interactive --agree-tos "
    "-m bfilipemv@gmail.com "
    "-d bva-p-socios.aobarcelos.pt "
    "2>&1",
    timeout=120
)
print(f"bva-p-socios cert:\n{out[-1500:]}\n")

# Testar nginx
out, _ = run("sudo nginx -t 2>&1")
print(f"Nginx -t: {out}")

run("sudo systemctl reload nginx")
time.sleep(2)

# Testar HTTPS
for domain, path in [
    ("aobarcelos.pt", "/"),
    ("www.aobarcelos.pt", "/"),
    ("bva-p.aobarcelos.pt", "/"),
    ("bva-p-socios.aobarcelos.pt", "/"),
]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 https://{domain}{path} 2>&1")
    print(f"HTTPS {domain}{path} -> {out}")

ssh.close()
