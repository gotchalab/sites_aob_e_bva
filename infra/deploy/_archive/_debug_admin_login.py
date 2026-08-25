import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=20):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Teste direto a /login sem seguir redirects
print("=== /login sem seguir redirects ===")
out, _ = run("curl -v --max-time 10 https://admin.aobarcelos.pt/login 2>&1 | grep -E 'HTTP|Location|< '| head -20")
print(out)

# Teste via localhost (sem SSL)
print("\n=== /login via localhost ===")
out, _ = run("curl -v --max-time 5 http://127.0.0.1:5001/login 2>&1 | grep -E 'HTTP|Location|< ' | head -20")
print(out)

# Verificar se o Blazor está a fazer HTTPS redirect
print("\n=== logs recentes do aob-admin ===")
out, _ = run("sudo journalctl -u aob-admin --since '5 min ago' --no-pager -l 2>&1 | tail -20")
print(out)

# Ver os headers que nginx envia para o backend
print("\n=== Teste com headers X-Forwarded corretos ===")
out, _ = run("curl -v --max-time 10 -H 'X-Forwarded-Proto: https' http://127.0.0.1:5001/login 2>&1 | grep -E 'HTTP|Location' | head -5")
print(out)

# Ver o response body de /login via curl direto
print("\n=== Body de /login (primeiras 5 linhas) ===")
out, _ = run("curl -s --max-time 10 http://127.0.0.1:5001/login 2>&1 | head -5")
print(out)

ssh.close()
