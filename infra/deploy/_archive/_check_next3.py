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

# Verificar se NODE_PATH esta no service
out, _ = run("sudo systemctl cat aob-aobarcelos | grep NODE_PATH")
print(f"NODE_PATH no service: '{out}'")

# Journal recente
out, _ = run("sudo journalctl -u aob-aobarcelos --since '2026-08-13 13:00:00' --no-pager")
print(f"\nJournal aobarcelos recente:\n{out[:1000]}")

out, _ = run("sudo journalctl -u aob-bva-portugal --since '2026-08-13 13:00:00' --no-pager")
print(f"\nJournal bva recente:\n{out[:500]}")

time.sleep(5)

# Curl com mais detalhe
out, _ = run("curl -sv --max-time 10 http://127.0.0.1:3001/ 2>&1 | tail -20")
print(f"\nCurl bva:\n{out[:500]}")

ssh.close()
