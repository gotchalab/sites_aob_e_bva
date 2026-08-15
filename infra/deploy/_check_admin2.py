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

# Estado do serviço admin
print("=== aob-admin service ===")
out, _ = run("sudo systemctl status aob-admin --no-pager -l 2>&1 | head -30")
print(out)

# Porto 5001
print("\n=== Porto 5001 ===")
out, _ = run("sudo ss -tlnp | grep 5001")
print(out or "(nada a escutar em 5001)")

# Teste direto ao admin via localhost
print("\n=== Teste http://127.0.0.1:5001 ===")
out, _ = run("curl -v --max-time 8 http://127.0.0.1:5001/ 2>&1 | head -30")
print(out)

# Nginx conf admin após certbot
print("\n=== nginx conf admin (após certbot) ===")
out, _ = run("cat /etc/nginx/sites-enabled/admin.aobarcelos.pt.conf")
print(out)

# Logs nginx para admin
print("\n=== nginx error.log (últimas 10 linhas) ===")
out, _ = run("sudo tail -10 /var/log/nginx/error.log 2>/dev/null")
print(out)

# Teste HTTPS com verbose
print("\n=== HTTPS admin (verbose) ===")
out, _ = run("curl -v --max-time 10 https://admin.aobarcelos.pt/ 2>&1 | head -40")
print(out)

ssh.close()
