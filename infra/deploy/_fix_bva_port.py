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

# Ver log do processo manual
out, _ = run("cat /tmp/bva_stdout.log 2>&1 | head -c 3000")
print(f"Log processo manual bva:\n{out}\n")

# Ver o que ocupa a porta 3001
out, _ = run("sudo ss -tlnp | grep 3001 2>&1")
print(f"Porta 3001: {out}")
out, _ = run("sudo fuser 3001/tcp 2>&1")
print(f"Fuser 3001: {out}")

# Matar TODOS os processos next que nao sao do systemd
out, _ = run("sudo pkill -9 -f 'next-server' 2>&1; echo exit=$?")
print(f"pkill next-server: {out}")

run("sudo systemctl stop aob-bva-portugal 2>&1")
time.sleep(2)

# Verificar que a porta esta livre
out, _ = run("sudo ss -tlnp | grep 3001 2>&1")
print(f"Porta 3001 apos kill: {out}")

# Reiniciar o servico
run("sudo systemctl start aob-bva-portugal")
time.sleep(6)

out, _ = run("sudo systemctl status aob-bva-portugal --no-pager -l | head -20")
print(f"\nStatus bva:\n{out}\n")

# Testar
out, _ = run("curl -sv --max-time 10 http://127.0.0.1:3001/ 2>&1 | grep -E 'HTTP|Error|error|Internal' | head -10")
print(f"Curl bva: {out}")

# Ver journal para erros de request
time.sleep(2)
out, _ = run("sudo journalctl -u aob-bva-portugal -n 20 --no-pager --output=cat")
print(f"\nJournal bva:\n{out}")

ssh.close()
