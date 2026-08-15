import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko, time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=30):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Journal do bvaproject
out, _ = run("sudo journalctl -u bvaproject -n 30 --no-pager --output=cat")
print(f"Journal bvaproject:\n{out}\n")

# Status
out, _ = run("sudo systemctl status bvaproject --no-pager -l | head -20")
print(f"Status:\n{out}\n")

# Porta
out, _ = run("sudo ss -tlnp | grep 5002")
print(f"Porta 5002: {out}\n")

# Ver se bvaproject-socios nginx existe
out, _ = run("ls /etc/nginx/sites-enabled/ 2>&1")
print(f"Nginx sites-enabled:\n{out}\n")

out, _ = run("sudo cat /etc/nginx/sites-enabled/bva-p-socios.aobarcelos.pt 2>/dev/null || sudo cat /etc/nginx/conf.d/bva*socios* 2>/dev/null || echo 'nao encontrado'")
print(f"Nginx bva-socios config:\n{out}\n")

# Aguardar um pouco e testar
time.sleep(5)
out, _ = run("sudo ss -tlnp | grep 5002")
print(f"Porta 5002 (5s depois): {out}")
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:5002/ 2>&1")
print(f"bvaproject HTTP: {out}")

ssh.close()
