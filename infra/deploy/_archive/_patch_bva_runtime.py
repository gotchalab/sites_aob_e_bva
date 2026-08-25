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

RUNTIME = Path("d:/PROJETOS/aob/frontends/bva-portugal/.next/server/webpack-runtime.js")

print("Enviando webpack-runtime.js corrigido...")
sftp = ssh.open_sftp()
sftp.put(str(RUNTIME), "/tmp/webpack-runtime-bva.js")
sftp.close()

run("sudo mv /tmp/webpack-runtime-bva.js /opt/aob/bva-portugal/.next/server/webpack-runtime.js")
run("sudo chown aob-web:aob-web /opt/aob/bva-portugal/.next/server/webpack-runtime.js")

# Verificar a linha corrigida
out, _ = run("grep -n 'return.*chunkId.*\\.js' /opt/aob/bva-portugal/.next/server/webpack-runtime.js")
print(f"Linha corrigida no VPS:\n{out}\n")

# Reiniciar servico
print("Reiniciando aob-bva-portugal...")
run("sudo systemctl restart aob-bva-portugal")
time.sleep(7)

out, _ = run("sudo systemctl is-active aob-bva-portugal")
print(f"Status: {out}")

# Testar
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/ 2>&1")
print(f"bva:3001 / -> HTTP {out}")

out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:3001/contacto 2>&1")
print(f"bva:3001 /contacto -> HTTP {out}")

out, _ = run("curl -s --max-time 10 http://127.0.0.1:3001/ 2>&1 | head -c 200")
print(f"\nBVA home preview:\n{out}")

# Testar via nginx
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 -H 'Host: bva-p.aobarcelos.pt' http://127.0.0.1/ 2>&1")
print(f"\nnginx bva-p / -> HTTP {out}")

# Journal
time.sleep(2)
out, _ = run("sudo journalctl -u aob-bva-portugal -n 10 --no-pager --output=cat")
print(f"\nJournal bva:\n{out}")

ssh.close()
