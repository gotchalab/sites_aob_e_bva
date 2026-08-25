import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, o, e = ssh.exec_command(cmd, timeout=20)
    return o.read().decode(errors="replace").strip()

endpoints = [
    "https://aobarcelos.pt/",
    "https://aobarcelos.pt/artigos",
    "https://bva-p.aobarcelos.pt/",
    "https://bva-p.aobarcelos.pt/artigos",
    "https://admin.aobarcelos.pt/",
    "https://api.aobarcelos.pt/health",
]

print("=== Verificacao final ===")
for url in endpoints:
    r = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 10 '{url}'")
    status = "OK" if r in ("200", "301", "302") else "PROBLEMA"
    print(f"  [{status}] {url} -> HTTP {r}")

print()
print("=== Servicos ===")
for svc in ["aob-api", "aob-admin", "aob-aobarcelos", "aob-bva-portugal"]:
    r = run(f"sudo systemctl is-active {svc}")
    print(f"  {svc}: {r}")

ssh.close()
