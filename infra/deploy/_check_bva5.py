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

# Verificar service file do bva - tem EnvironmentFile?
out, _ = run("sudo systemctl cat aob-bva-portugal | head -30")
print(f"Service file bva:\n{out}\n")

# Ver env vars do processo bva (todas)
pid_out, _ = run("sudo systemctl show -p MainPID aob-bva-portugal")
pid = pid_out.split("=")[1].strip()
out, _ = run(f"sudo cat /proc/{pid}/environ 2>/dev/null | tr '\\0' '\\n' | grep -v '^$'")
print(f"Env vars processo bva (PID {pid}):\n{out}\n")

# Fazer curl e depois ver journal
run("curl --max-time 5 http://127.0.0.1:3001/ > /dev/null 2>&1 &")
time.sleep(3)
out, _ = run("sudo journalctl -u aob-bva-portugal --since '2026-08-13 13:25:00' --no-pager -p 0..6")
print(f"Journal bva recente (all levels):\n{out}\n")

# Ver todos os logs (incluindo stderr do node)
out, _ = run("sudo journalctl -u aob-bva-portugal -n 50 --no-pager --output=cat")
print(f"Journal bva (50 linhas, cat format):\n{out}\n")

ssh.close()
