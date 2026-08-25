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

for cmd in [
    "sudo systemctl daemon-reload",
    "sudo systemctl restart aob-aobarcelos aob-bva-portugal",
]:
    out, err = run(cmd)
    if out: print(out)
    if err: print("[err]", err[:200])

print("Aguardar 20s para Next.js inicializar...")
time.sleep(20)

for cmd in [
    "sudo journalctl -u aob-aobarcelos -n 15 --no-pager",
    "sudo journalctl -u aob-bva-portugal -n 5 --no-pager",
    "curl -sf --max-time 10 http://127.0.0.1:3000/ | head -c 100",
    "curl -sf --max-time 10 http://127.0.0.1:3001/ | head -c 100",
]:
    print(f"\n=== {cmd[:70]} ===")
    out, err = run(cmd)
    if out: print(out[:400])
    if err: print("[err]", err[:200])

ssh.close()
