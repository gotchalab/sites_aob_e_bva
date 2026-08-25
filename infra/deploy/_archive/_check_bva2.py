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

# Env vars do bva
out, _ = run("sudo cat /etc/aob/bva-portugal.env 2>&1")
print(f"Env bva:\n{out}\n")

# Env vars do aobarcelos
out, _ = run("sudo cat /etc/aob/aobarcelos.env 2>&1")
print(f"Env aobarcelos:\n{out}\n")

# Curl bva com header Host correcto
out, _ = run("curl -sv --max-time 10 -H 'Host: bva-p.aobarcelos.pt' http://127.0.0.1:3001/ 2>&1 | tail -30")
print(f"Curl bva com Host header:\n{out}\n")

# Verificar ficheiros next do bva
out, _ = run("ls -la /opt/aob/bva-portugal/ 2>&1")
print(f"Ficheiros bva:\n{out}\n")

out, _ = run("ls /opt/aob/bva-portugal/.next/server/app/ 2>&1 | head -20")
print(f".next/server/app/:\n{out}\n")

ssh.close()
