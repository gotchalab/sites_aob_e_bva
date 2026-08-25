import sys, io, secrets, base64
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=30):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Verificar se ha firewall (ufw)
out, _ = run("sudo ufw status 2>&1")
print(f"UFW status:\n{out}\n")

# Ver todos os appsettings do bvaproject
out, _ = run("sudo ls /home/bva/bvaproject/ 2>&1")
print(f"Ficheiros bvaproject:\n{out}\n")

out, _ = run("sudo grep -r 'Jwt\\|jwt\\|Secret\\|secret\\|Key\\|key' /home/bva/bvaproject/appsettings*.json 2>&1 | grep -v Password | grep -v Database | head -20")
print(f"JWT/Keys no bvaproject:\n{out}\n")

# Corrigir o servico para escutar apenas em localhost
print("Corrigindo bvaproject para escutar em localhost...")
run("sudo sed -i 's|--server.urls=http://\\*:5002|--server.urls=http://127.0.0.1:5002|g' /etc/systemd/system/bvaproject.service")

out, _ = run("sudo grep 'server.urls' /etc/systemd/system/bvaproject.service")
print(f"Depois da correcao: {out}")

run("sudo systemctl daemon-reload")
run("sudo systemctl restart bvaproject")

import time; time.sleep(5)

out, _ = run("sudo systemctl is-active bvaproject")
print(f"Status bvaproject: {out}")

out, _ = run("sudo ss -tlnp | grep 5002")
print(f"Porta 5002 apos restart: {out}")

# Verificar curl
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:5002/ 2>&1")
print(f"bvaproject:5002 / -> HTTP {out}")

ssh.close()
