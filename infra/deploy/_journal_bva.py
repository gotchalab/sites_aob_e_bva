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

# Fazer um request
run("curl -s --max-time 5 http://127.0.0.1:3001/ > /dev/null 2>&1")
time.sleep(2)

# Ver journal completo
out, _ = run("sudo journalctl -u aob-bva-portugal -n 50 --no-pager --output=cat")
print(f"Journal bva:\n{out}\n")

# Ver se ha erros na pasta .next
out, _ = run("sudo journalctl -u aob-bva-portugal --since '2026-08-13 13:40:00' --no-pager")
print(f"Journal bva desde 13:40:\n{out}\n")

ssh.close()
