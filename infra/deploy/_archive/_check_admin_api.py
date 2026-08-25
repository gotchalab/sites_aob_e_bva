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

print("=== NGINX: admin.aobarcelos.pt.conf ===")
out, _ = run("cat /etc/nginx/sites-enabled/admin.aobarcelos.pt.conf")
print(out)

print("\n=== NGINX: api.aobarcelos.pt.conf ===")
out, _ = run("cat /etc/nginx/sites-enabled/api.aobarcelos.pt.conf")
print(out)

print("\n=== DNS resolution (do VPS) ===")
for domain in ["admin.aobarcelos.pt", "api.aobarcelos.pt"]:
    out, _ = run(f"dig +short {domain} 2>/dev/null || host {domain} 2>/dev/null | head -2")
    print(f"  {domain}: {out or '(sem resolução)'}")

print("\n=== Teste HTTP (sem SSL) ===")
for domain in ["admin.aobarcelos.pt", "api.aobarcelos.pt"]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 8 http://{domain}/ 2>&1")
    print(f"  http://{domain}/ -> {out}")

print("\n=== Teste HTTPS (com SSL se existir) ===")
for domain in ["admin.aobarcelos.pt", "api.aobarcelos.pt"]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 8 https://{domain}/ 2>&1")
    print(f"  https://{domain}/ -> {out}")

ssh.close()
