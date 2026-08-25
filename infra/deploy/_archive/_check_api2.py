import paramiko
import time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Journal API (ultimas linhas)
out, _ = run("sudo journalctl -u aob-api --since '2026-08-13 13:00:00' --no-pager | tail -30")
print("=== API journal ===")
print(out[:1500])

# Testar rotas da API directamente
for path in ("/api/health", "/api/sites/aob", "/api/sites/aob/articles?pageSize=1",
             "/api/sites/bva/articles?pageSize=1"):
    out, _ = run(f"curl -sf --max-time 5 http://127.0.0.1:5000{path} 2>&1 | head -c 200")
    print(f"\nGET {path}: {out[:200]}")

ssh.close()
