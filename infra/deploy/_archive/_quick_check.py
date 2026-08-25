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

# Estado de todos os servicos
out, _ = run("sudo systemctl is-active aob-api aob-admin aob-aobarcelos aob-bva-portugal nginx 2>&1")
print(f"Estado servicos:\n{out}\n")

# Reiniciar bva limpo
run("sudo systemctl restart aob-bva-portugal")
time.sleep(6)

out, _ = run("sudo systemctl is-active aob-bva-portugal")
print(f"bva status: {out}")

# Testar aobarcelos directamente no porto 3000
out, _ = run("curl -s --max-time 10 http://127.0.0.1:3000/ 2>&1 | head -c 100")
print(f"aobarcelos:3000 / -> {out[:80]}")

# Testar bva directamente no porto 3001
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/ 2>&1")
print(f"bva:3001 / -> HTTP {out}")

out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/contacto 2>&1")
print(f"bva:3001 /contacto -> HTTP {out}")

# Testar via nginx
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 -H 'Host: aobarcelos.pt' http://127.0.0.1/ 2>&1")
print(f"nginx aobarcelos.pt / -> HTTP {out}")

out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 -H 'Host: bva-p.aobarcelos.pt' http://127.0.0.1/ 2>&1")
print(f"nginx bva-p / -> HTTP {out}")

# Journal bva (30 linhas mais recentes)
out, _ = run("sudo journalctl -u aob-bva-portugal -n 30 --no-pager --output=short-iso")
print(f"\nJournal bva:\n{out}\n")

ssh.close()
