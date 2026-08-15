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

print("=== ESTADO DOS SERVICOS ===")
out, _ = run("sudo systemctl is-active aob-api aob-admin aob-aobarcelos aob-bva-portugal nginx")
print(out)

print("\n=== TESTES HTTP ===")
tests = [
    ("API /sites/aob", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:5000/api/sites/aob"),
    ("API /sites/bva", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:5000/api/sites/bva"),
    ("aobarcelos:3000 /", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:3000/"),
    ("bva:3001 /", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:3001/"),
    ("nginx aobarcelos.pt /", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 -H 'Host: aobarcelos.pt' http://127.0.0.1/"),
    ("nginx bva-p /", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 -H 'Host: bva-p.aobarcelos.pt' http://127.0.0.1/"),
    ("nginx api /health", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 -H 'Host: api.aobarcelos.pt' http://127.0.0.1/api/sites/aob"),
    ("nginx admin /", "curl -s -o /dev/null -w '%{http_code}' --max-time 5 -H 'Host: admin.aobarcelos.pt' http://127.0.0.1/"),
]

for label, cmd in tests:
    out, _ = run(cmd)
    print(f"  {label}: HTTP {out}")

print("\n=== PORTAS ABERTAS ===")
out, _ = run("sudo ss -tlnp | grep -E ':80|:443|:3000|:3001|:5000|:5135'")
print(out)

print("\n=== DNS CHECK (aobarcelos.pt) ===")
out, _ = run("dig +short aobarcelos.pt 2>&1 | head -3")
print(f"aobarcelos.pt -> {out}")

out, _ = run("dig +short bva-p.aobarcelos.pt 2>&1 | head -3")
print(f"bva-p.aobarcelos.pt -> {out}")

out, _ = run("dig +short api.aobarcelos.pt 2>&1 | head -3")
print(f"api.aobarcelos.pt -> {out}")

print("\n=== IP DO VPS ===")
out, _ = run("curl -s --max-time 5 https://ipecho.net/plain 2>&1")
print(f"IP publico VPS: {out}")

ssh.close()
