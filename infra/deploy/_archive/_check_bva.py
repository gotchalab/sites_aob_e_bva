import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Estado dos servicos
out, _ = run("sudo systemctl status aob-bva-portugal --no-pager -l | head -30")
print(f"Status bva:\n{out}\n")

# Journal bva
out, _ = run("sudo journalctl -u aob-bva-portugal -n 30 --no-pager")
print(f"Journal bva:\n{out[-3000:]}\n")

# Curl directo ao porto 3001
out, _ = run("curl -sv --max-time 10 http://127.0.0.1:3001/ 2>&1 | tail -30")
print(f"Curl bva porta 3001:\n{out}\n")

# Health do API
out, _ = run("curl -sv --max-time 10 http://127.0.0.1:5000/api/health 2>&1 | tail -20")
print(f"API health verbose:\n{out}")

ssh.close()
