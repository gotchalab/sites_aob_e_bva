import sys, io, json, secrets, base64
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

# Ver o appsettings actual do bvaproject
out, _ = run("sudo cat /home/bva/bvaproject/appsettings.Production.json 2>&1")
print(f"bvaproject appsettings.Production.json:\n{out}\n")

# Ver o que porta e url o bvaproject usa
out, _ = run("sudo systemctl cat bvaproject 2>/dev/null || sudo systemctl cat bva-project 2>/dev/null || echo 'servico nao encontrado'")
print(f"bvaproject service:\n{out}\n")

out, _ = run("sudo ss -tlnp | grep 5002")
print(f"Porta 5002: {out}\n")

ssh.close()
