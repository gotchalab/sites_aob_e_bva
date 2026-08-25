import paramiko
from pathlib import Path
import time

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

# Aguardar que Next.js inicialize
print("A aguardar 15s para Next.js inicializar...")
time.sleep(15)

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

tests = [
    ("Portas em escuta",
     "sudo ss -tlnp | grep -E ':(3000|3001|5000|5001|80)' | awk '{print $4, $6}'"),
    ("Frontend aobarcelos (3000)",
     "curl -s --max-time 10 http://127.0.0.1:3000/ | head -c 200"),
    ("Frontend bva (3001)",
     "curl -s --max-time 10 http://127.0.0.1:3001/ | head -c 200"),
    ("Admin (5001)",
     "curl -s --max-time 10 http://127.0.0.1:5001/ | head -c 200"),
    ("Nginx (80) host aobarcelos.pt",
     "curl -s --max-time 10 http://127.0.0.1/ -H 'Host: aobarcelos.pt' | head -c 200"),
    ("journalctl aob-aobarcelos (ultimas 10 linhas)",
     "sudo journalctl -u aob-aobarcelos -n 10 --no-pager"),
    ("journalctl aob-bva-portugal (ultimas 10 linhas)",
     "sudo journalctl -u aob-bva-portugal -n 10 --no-pager"),
]

for name, cmd in tests:
    print(f"\n=== {name} ===")
    out, err = run(cmd)
    if out:
        print(out[:500])
    if err:
        print("[err]", err[:300])

ssh.close()
