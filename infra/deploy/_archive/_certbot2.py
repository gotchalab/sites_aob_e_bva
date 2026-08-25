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

# Instalar python3-certbot-nginx
print("Instalando python3-certbot-nginx...")
out, err = run("sudo apt-get install -y python3-certbot-nginx 2>&1 | tail -10", timeout=120)
print(f"Instalacao: {out[-500:]}\n")

# Verificar
out, _ = run("sudo certbot plugins 2>&1 | grep nginx")
print(f"Plugins certbot: {out}\n")

# Correr certbot para aobarcelos.pt + www
print("Certbot aobarcelos.pt + www...")
out, _ = run(
    "sudo certbot --nginx --non-interactive --agree-tos "
    "-m bfilipemv@gmail.com "
    "-d aobarcelos.pt -d www.aobarcelos.pt 2>&1",
    timeout=120
)
print(f"{out[-2000:]}\n")

# Correr certbot para bva-p.aobarcelos.pt
print("Certbot bva-p.aobarcelos.pt...")
out, _ = run(
    "sudo certbot --nginx --non-interactive --agree-tos "
    "-m bfilipemv@gmail.com "
    "-d bva-p.aobarcelos.pt 2>&1",
    timeout=120
)
print(f"{out[-1500:]}\n")

# Correr certbot para bva-p-socios.aobarcelos.pt
print("Certbot bva-p-socios.aobarcelos.pt...")
out, _ = run(
    "sudo certbot --nginx --non-interactive --agree-tos "
    "-m bfilipemv@gmail.com "
    "-d bva-p-socios.aobarcelos.pt 2>&1",
    timeout=120
)
print(f"{out[-1500:]}\n")

# Reload nginx
out, _ = run("sudo nginx -t 2>&1 && sudo systemctl reload nginx 2>&1")
print(f"Nginx reload: {out}")

time.sleep(2)

# Testar HTTPS
print("\nTestes HTTPS:")
for domain in ["aobarcelos.pt", "www.aobarcelos.pt", "bva-p.aobarcelos.pt", "bva-p-socios.aobarcelos.pt"]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 https://{domain}/ 2>&1")
    print(f"  https://{domain}/ -> {out}")

ssh.close()
