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

# Ver estrutura do .next/static do bva
out, _ = run("ls /opt/aob/bva-portugal/.next/static/ 2>&1")
print(f".next/static/:\n{out}\n")

out, _ = run("ls /opt/aob/bva-portugal/.next/static/media/ 2>&1 | head -10")
print(f".next/static/media/ (fontes):\n{out}\n")

# Comparar com aobarcelos
out, _ = run("ls /opt/aob/aobarcelos/.next/static/media/ 2>&1 | head -10")
print(f"aobarcelos .next/static/media/:\n{out}\n")

# Ver o build-manifest do bva
out, _ = run("cat /opt/aob/bva-portugal/.next/build-manifest.json 2>&1 | head -c 500")
print(f"build-manifest.json:\n{out}\n")

# Ver routes manifest
out, _ = run("cat /opt/aob/bva-portugal/.next/routes-manifest.json 2>&1 | head -c 1000")
print(f"routes-manifest.json:\n{out}\n")

# Parar bva e correr com NODE_ENV=development para ver erro detalhado
run("sudo systemctl stop aob-bva-portugal")
time.sleep(1)

run("""sudo -u aob-web bash -c 'cd /opt/aob/bva-portugal && NODE_PATH=/usr/lib/node_modules NODE_ENV=production API_INTERNAL_URL=http://127.0.0.1:5000 NEXT_PUBLIC_SITE_SLUG=bva NEXT_PUBLIC_API_URL=https://bva-p.aobarcelos.pt NEXT_PUBLIC_TURNSTILE_SITEKEY=1x00000000000000000000AA /usr/bin/next start -p 3001 -H 127.0.0.1 > /tmp/bva_prod.log 2>&1 &'""")
time.sleep(5)

# Fazer request e ver resposta completa
out, _ = run("curl -s --max-time 10 http://127.0.0.1:3001/ 2>&1")
print(f"Resposta completa bva:\n{out[:1000]}\n")

# Ver log
out, _ = run("cat /tmp/bva_prod.log 2>&1")
print(f"Log manual bva:\n{out[:3000]}\n")

# Matar
run("sudo pkill -9 -f 'next-server' 2>/dev/null; true")
time.sleep(1)
run("sudo systemctl start aob-bva-portugal")

ssh.close()
