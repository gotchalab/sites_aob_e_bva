import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Parar servico bva e correr manualmente para ver erros
run("sudo systemctl stop aob-bva-portugal")

# Correr next start manualmente como aob-web com as env vars
import time; time.sleep(1)

# Iniciar em background e aguardar
run("sudo -u aob-web bash -c 'cd /opt/aob/bva-portugal && NODE_PATH=/usr/lib/node_modules NEXT_PUBLIC_API_URL=https://bva-p.aobarcelos.pt NEXT_PUBLIC_SITE_SLUG=bva API_INTERNAL_URL=http://127.0.0.1:5000 NEXT_PUBLIC_TURNSTILE_SITEKEY=1x00000000000000000000AA /usr/bin/next start -p 3001 -H 127.0.0.1 > /tmp/bva_stdout.log 2>&1 &'")
time.sleep(4)

# Fazer curl
out, _ = run("curl -sv --max-time 10 http://127.0.0.1:3001/ 2>&1 | grep -E '< HTTP|body|Error|error' | head -20")
print(f"Curl result: {out}\n")

time.sleep(1)

# Ver logs
out, _ = run("cat /tmp/bva_stdout.log 2>&1")
print(f"STDOUT/STDERR do next start:\n{out[-4000:]}\n")

# Matar processo manual e reiniciar servico
run("sudo pkill -f 'next start.*3001' 2>/dev/null; true")
time.sleep(1)
run("sudo systemctl start aob-bva-portugal")

ssh.close()
