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

# Comparar server/ directories
out, _ = run("ls /opt/aob/bva-portugal/.next/server/ 2>&1")
print(f"bva .next/server/:\n{out}\n")

out, _ = run("ls /opt/aob/aobarcelos/.next/server/ 2>&1")
print(f"aobarcelos .next/server/:\n{out}\n")

# Comparar chunks
out, _ = run("ls /opt/aob/bva-portugal/.next/server/chunks/ 2>&1 | head -20")
print(f"bva server/chunks/:\n{out}\n")

out, _ = run("ls /opt/aob/aobarcelos/.next/server/chunks/ 2>&1 | head -20")
print(f"aobarcelos server/chunks/:\n{out}\n")

# Ver o app-paths-manifest do bva
out, _ = run("cat /opt/aob/bva-portugal/.next/server/app-paths-manifest.json 2>&1")
print(f"bva app-paths-manifest.json:\n{out}\n")

# Ver o next-font-manifest do bva
out, _ = run("cat /opt/aob/bva-portugal/.next/server/next-font-manifest.json 2>&1 | head -c 500")
print(f"bva next-font-manifest.json:\n{out}\n")

# Verificar se ha erros no trace
out, _ = run("ls /opt/aob/bva-portugal/.next/ 2>&1")
print(f"bva .next/ root:\n{out}\n")

# Tentar correr next start com MAIS verbosidade
# Parar servico bva
run("sudo systemctl stop aob-bva-portugal")
time.sleep(1)

# Correr e capturar o que aparece ao fazer um pedido
run("""sudo bash -c 'cd /opt/aob/bva-portugal && sudo -u aob-web env NODE_PATH=/usr/lib/node_modules API_INTERNAL_URL=http://127.0.0.1:5000 NEXT_PUBLIC_SITE_SLUG=bva NEXT_PUBLIC_API_URL=https://bva-p.aobarcelos.pt NEXT_PUBLIC_TURNSTILE_SITEKEY=1x00000000000000000000AA /usr/bin/next start -p 3001 -H 127.0.0.1 > /tmp/bva2.log 2>&1 </dev/null &'""", timeout=5)
time.sleep(5)

run("curl -s --max-time 5 http://127.0.0.1:3001/ > /dev/null 2>&1 &")
time.sleep(3)

out, _ = run("cat /tmp/bva2.log 2>&1")
print(f"Log next start bva:\n{out[:3000]}\n")

run("sudo pkill -9 -f 'next-server' 2>/dev/null; true", timeout=5)
time.sleep(1)
run("sudo systemctl start aob-bva-portugal", timeout=10)

ssh.close()
