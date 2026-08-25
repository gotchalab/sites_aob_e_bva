import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=30):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Verificar se page.js existe no VPS
out, _ = run("ls /opt/aob/bva-portugal/.next/server/app/ 2>&1")
print(f"bva server/app/ no VPS:\n{out}\n")

# Ver o app-paths-manifest no VPS vs local
out, _ = run("cat /opt/aob/bva-portugal/.next/server/app-paths-manifest.json 2>&1")
print(f"app-paths-manifest no VPS:\n{out}\n")

# Ver o app-paths-manifest do aobarcelos no VPS
out, _ = run("cat /opt/aob/aobarcelos/.next/server/app-paths-manifest.json 2>&1")
print(f"aobarcelos app-paths-manifest no VPS:\n{out}\n")

ssh.close()
