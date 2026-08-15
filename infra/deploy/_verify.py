import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

tests = [
    # API health
    ("API porta 5000",
     "curl -sf http://127.0.0.1:5000/health 2>&1 || curl -sf http://127.0.0.1:5000/api/health 2>&1 || curl -I http://127.0.0.1:5000/ 2>&1 | head -3"),
    # Frontend aobarcelos porta 3000
    ("Frontend aobarcelos porta 3000",
     "curl -sf http://127.0.0.1:3000/ 2>&1 | head -3"),
    # Frontend bva porta 3001
    ("Frontend bva porta 3001",
     "curl -sf http://127.0.0.1:3001/ 2>&1 | head -3"),
    # Admin porta 5001
    ("Admin porta 5001",
     "curl -sf http://127.0.0.1:5001/ 2>&1 | head -3"),
    # Nginx porta 80 (via IP)
    ("Nginx porta 80",
     "curl -sf http://127.0.0.1:80/ -H 'Host: aobarcelos.pt' 2>&1 | head -3"),
    # Estado dos servicos
    ("Estado servicos",
     "systemctl is-active aob-api aob-admin aob-aobarcelos aob-bva-portugal nginx"),
]

for name, cmd in tests:
    print(f"\n=== {name} ===")
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    if out:
        print(out[:300])
    if err:
        print("[err]", err[:200])

ssh.close()
