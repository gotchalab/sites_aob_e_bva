import sys, io
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

# Ver config do bva-socios
out, _ = run("sudo cat /etc/nginx/sites-enabled/bva-p-socios.aobarcelos.pt.conf 2>&1")
print(f"bva-socios nginx config:\n{out}\n")

# Testar via nginx
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 5 -H 'Host: bva-p-socios.aobarcelos.pt' http://127.0.0.1/ 2>&1")
print(f"nginx bva-socios / -> HTTP {out}")

# Ver o nginx.conf principal
out, _ = run("sudo nginx -T 2>&1 | grep -E 'server_name|proxy_pass|listen' | head -30")
print(f"\nNginx server_name e proxy_pass:\n{out}\n")

# Teste de syntax
out, _ = run("sudo nginx -t 2>&1")
print(f"Nginx -t:\n{out}\n")

ssh.close()
